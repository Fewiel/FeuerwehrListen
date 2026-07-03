using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;

namespace FeuerwehrListen.Repositories;

public class OperationReportRepository
{
    private readonly AppDbConnection _db;

    public OperationReportRepository(AppDbConnection db)
    {
        _db = db;
    }

    public async Task<OperationReport?> GetByOperationListIdAsync(int operationListId)
    {
        return await _db.OperationReports
            .FirstOrDefaultAsync(x => x.OperationListId == operationListId);
    }

    /// <summary>
    /// Liefert den Bericht zur Einsatzliste oder legt einen neuen (leeren) an.
    /// </summary>
    public async Task<OperationReport> GetOrCreateAsync(int operationListId)
    {
        var existing = await GetByOperationListIdAsync(operationListId);
        if (existing != null)
            return existing;

        var report = new OperationReport
        {
            OperationListId = operationListId,
            CreatedAt = DateTime.Now
        };
        report.Id = await _db.InsertWithInt32IdentityAsync(report);
        return report;
    }

    public async Task UpdateAsync(OperationReport report)
    {
        report.UpdatedAt = DateTime.Now;
        await _db.UpdateAsync(report);
    }

    public async Task DeleteByOperationListIdAsync(int operationListId)
    {
        var report = await GetByOperationListIdAsync(operationListId);
        if (report != null)
        {
            await _db.OperationReportExternalForces.Where(x => x.OperationReportId == report.Id).DeleteAsync();
            await _db.OperationReportMittels.Where(x => x.OperationReportId == report.Id).DeleteAsync();
            await _db.OperationReportVehicleStrengths.Where(x => x.OperationReportId == report.Id).DeleteAsync();
        }
        await _db.OperationReports.Where(x => x.OperationListId == operationListId).DeleteAsync();
    }

    // --- Fahrzeug-Stärken ---

    public async Task<List<OperationReportVehicleStrength>> GetVehicleStrengthsAsync(int reportId)
    {
        return await _db.OperationReportVehicleStrengths
            .Where(x => x.OperationReportId == reportId)
            .OrderBy(x => x.Id)
            .ToListAsync();
    }

    /// <summary>Ersetzt alle Fahrzeug-Stärken eines Berichts (nur nicht-leere Einträge).</summary>
    public async Task ReplaceVehicleStrengthsAsync(int reportId, IEnumerable<OperationReportVehicleStrength> items)
    {
        await _db.OperationReportVehicleStrengths.Where(x => x.OperationReportId == reportId).DeleteAsync();
        foreach (var s in items)
        {
            if (string.IsNullOrWhiteSpace(s.VehicleName) || string.IsNullOrWhiteSpace(s.Staerke)) continue;
            s.OperationReportId = reportId;
            await _db.InsertAsync(s);
        }
    }

    /// <summary>Setzt/aktualisiert die Stärke eines einzelnen Fahrzeugs (für Nachkorrektur beim Abschließen).</summary>
    public async Task UpsertVehicleStrengthAsync(int reportId, string vehicleName, string staerke)
    {
        var existing = await _db.OperationReportVehicleStrengths
            .FirstOrDefaultAsync(x => x.OperationReportId == reportId && x.VehicleName == vehicleName);
        if (existing != null)
        {
            existing.Staerke = staerke;
            await _db.UpdateAsync(existing);
        }
        else
        {
            await _db.InsertAsync(new OperationReportVehicleStrength
            {
                OperationReportId = reportId,
                VehicleName = vehicleName,
                Staerke = staerke
            });
        }
    }

    // --- Externe Kräfte ---

    public async Task<List<OperationReportExternalForce>> GetExternalForcesAsync(int reportId)
    {
        return await _db.OperationReportExternalForces
            .Where(x => x.OperationReportId == reportId)
            .OrderBy(x => x.Id)
            .ToListAsync();
    }

    public async Task<int> InsertExternalForceAsync(OperationReportExternalForce force)
    {
        return await _db.InsertWithInt32IdentityAsync(force);
    }

    public async Task UpdateExternalForceAsync(OperationReportExternalForce force)
    {
        await _db.UpdateAsync(force);
    }

    public async Task DeleteExternalForceAsync(int id)
    {
        await _db.OperationReportExternalForces.Where(x => x.Id == id).DeleteAsync();
    }

    // --- Eingesetzte Mittel ---

    public async Task<List<OperationReportMittel>> GetMittelAsync(int reportId)
    {
        return await _db.OperationReportMittels
            .Where(x => x.OperationReportId == reportId)
            .OrderBy(x => x.Id)
            .ToListAsync();
    }

    /// <summary>Ersetzt die komplette Mittel-Liste eines Berichts (delete + insert).</summary>
    public async Task ReplaceMittelAsync(int reportId, IEnumerable<OperationReportMittel> mittel)
    {
        await _db.OperationReportMittels.Where(x => x.OperationReportId == reportId).DeleteAsync();
        foreach (var m in mittel)
        {
            m.OperationReportId = reportId;
            await _db.InsertAsync(m);
        }
    }
}
