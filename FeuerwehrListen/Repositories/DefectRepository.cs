using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;

namespace FeuerwehrListen.Repositories;

public class DefectRepository
{
    private readonly AppDbConnection _db;

    public DefectRepository(AppDbConnection db)
    {
        _db = db;
    }

    public async Task<int> GetCountAsync(DefectStatus? statusFilter = null)
    {
        var query = _db.Defects.AsQueryable();
        if (statusFilter.HasValue)
            query = query.Where(d => d.Status == statusFilter.Value);
        return await query.CountAsync();
    }

    public async Task<List<Defect>> GetPagedAsync(int page, int pageSize, DefectStatus? statusFilter = null)
    {
        var query = _db.Defects.AsQueryable();
        if (statusFilter.HasValue)
            query = query.Where(d => d.Status == statusFilter.Value);
        return await query
            .OrderByDescending(d => d.ReportedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<Defect?> GetByIdAsync(int id)
    {
        return await _db.Defects.FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<int> CreateAsync(Defect defect)
    {
        return await _db.InsertWithInt32IdentityAsync(defect);
    }

    public async Task UpdateAsync(Defect defect)
    {
        await _db.UpdateAsync(defect);
    }

    public async Task<List<DefectStatusChange>> GetStatusChangesAsync(int defectId)
    {
        return await _db.DefectStatusChanges
            .Where(c => c.DefectId == defectId)
            .OrderByDescending(c => c.ChangedAt)
            .ToListAsync();
    }

    public async Task AddStatusChangeAsync(DefectStatusChange change)
    {
        await _db.InsertAsync(change);
    }
}
