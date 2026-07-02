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
        await _db.OperationReports.Where(x => x.OperationListId == operationListId).DeleteAsync();
    }
}
