using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;

namespace FeuerwehrListen.Repositories;

public class AttendanceListRepository
{
    private readonly AppDbConnection _db;

    public AttendanceListRepository(AppDbConnection db)
    {
        _db = db;
    }

    public async Task<List<AttendanceList>> GetAllAsync()
    {
        // Sitzungs-Anwesenheiten des Fuehrungskreises gehoeren nicht in Listen/Statistik.
        return await _db.AttendanceLists.Where(x => x.LeadershipProtocolId == null).ToListAsync();
    }

    public async Task<List<AttendanceList>> GetOpenAsync()
    {
        return await _db.AttendanceLists
            .Where(x => x.LeadershipProtocolId == null && x.Status == ListStatus.Open && !x.IsArchived)
            .ToListAsync();
    }

    public async Task<List<AttendanceList>> GetClosedAsync()
    {
        return await _db.AttendanceLists
            .Where(x => x.LeadershipProtocolId == null && x.Status == ListStatus.Closed && !x.IsArchived)
            .ToListAsync();
    }

    public async Task<List<AttendanceList>> GetArchivedAsync()
    {
        return await _db.AttendanceLists
            .Where(x => x.LeadershipProtocolId == null && x.IsArchived)
            .ToListAsync();
    }

    public async Task<AttendanceList?> GetByIdAsync(int id)
    {
        return await _db.AttendanceLists
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<AttendanceList?> GetOpenByUnitNumberAsync(int unitNumber)
    {
        return await _db.AttendanceLists
            .Where(x => x.LeadershipProtocolId == null && x.Status == ListStatus.Open && !x.IsArchived && x.UnitNumber == unitNumber)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<List<AttendanceList>> GetAllOpenByUnitNumberAsync(int unitNumber)
    {
        return await _db.AttendanceLists
            .Where(x => x.LeadershipProtocolId == null && x.Status == ListStatus.Open && !x.IsArchived && x.UnitNumber == unitNumber)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<AttendanceList>> GetOpenWithoutUnitAsync()
    {
        return await _db.AttendanceLists
            .Where(x => x.LeadershipProtocolId == null && x.Status == ListStatus.Open && !x.IsArchived && x.UnitNumber == null)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> CreateAsync(AttendanceList list)
    {
        var id = await _db.InsertWithInt32IdentityAsync(list);
        list.Id = id;
        // Im Voraus gemeldete Abwesenheiten gleich als "entschuldigt" uebernehmen (nicht Fuehrungskreis).
        if (list.LeadershipProtocolId == null) await ApplyAbsencesAsync(list);
        return id;
    }

    /// <summary>Einheiten eines Mitglieds wie UnitAssignmentService: Zuordnungstabelle, Haupteinheit,
    /// sonst aus der Mitgliedsnummer (1xxx = Einheit 1).</summary>
    private async Task<HashSet<int>> UnitsOfAsync(Member m)
    {
        var units = (await _db.MemberUnits.Where(x => x.MemberId == m.Id).Select(x => x.UnitNumber).ToListAsync()).ToHashSet();
        if (m.UnitNumber is int u) units.Add(u);
        if (units.Count == 0 && int.TryParse(m.MemberNumber, out var n) && n > 999 && n / 1000 is >= 1 and <= 9) units.Add(n / 1000);
        return units;
    }

    /// <summary>Angenommene Laufzeit einer Liste fuer den Abgleich mit Abwesenheiten:
    /// Oeffnung bis Oeffnung + 3 Stunden (eine Liste um 18:30 trifft so eine Meldung 19-21 Uhr).</summary>
    public static readonly TimeSpan AssumedDuration = TimeSpan.FromHours(3);

    /// <summary>Alle Abwesenheiten, die den Zeitraum der Liste ueberschneiden, als entschuldigt eintragen.</summary>
    public async Task<int> ApplyAbsencesAsync(AttendanceList list)
    {
        var start = list.CreatedAt; var end = start + AssumedDuration;
        var absences = await _db.MemberAbsences.Where(a => a.From < end && a.To > start).ToListAsync();
        if (absences.Count == 0) return 0;
        var entries = await _db.AttendanceEntries.Where(e => e.AttendanceListId == list.Id).ToListAsync();
        var added = 0;
        foreach (var memberId in absences.Select(a => a.MemberId).Distinct())
        {
            var m = await _db.Members.FirstOrDefaultAsync(x => x.Id == memberId);
            if (m == null || entries.Any(e => e.NameOrId.Contains($"({m.MemberNumber})"))) continue;
            // Einheiten-Liste: nur Mitglieder dieser Einheit (sonst landet die Entschuldigung bei
            // gleichzeitig offenen Listen von Einheit 1 und 2 doppelt). Listen ohne Einheit: alle.
            if (list.UnitNumber is int unit && !(await UnitsOfAsync(m)).Contains(unit)) continue;
            await _db.InsertAsync(new AttendanceEntry
            {
                AttendanceListId = list.Id, NameOrId = $"{m.FirstName} {m.LastName} ({m.MemberNumber})",
                EnteredAt = DateTime.Now, IsExcused = true
            });
            added++;
        }
        return added;
    }

    public async Task UpdateAsync(AttendanceList list)
    {
        await _db.UpdateAsync(list);
    }

    public async Task DeleteAsync(int id)
    {
        await _db.AttendanceEntries
            .Where(x => x.AttendanceListId == id)
            .DeleteAsync();
        await _db.AttendanceLists
            .Where(x => x.Id == id)
            .DeleteAsync();
    }
}




