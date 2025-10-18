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
        return await _db.AttendanceLists.ToListAsync();
    }

    public async Task<List<AttendanceList>> GetOpenAsync()
    {
        return await _db.AttendanceLists
            .Where(x => x.Status == ListStatus.Open && !x.IsArchived)
            .ToListAsync();
    }

    public async Task<List<AttendanceList>> GetClosedAsync()
    {
        return await _db.AttendanceLists
            .Where(x => x.Status == ListStatus.Closed && !x.IsArchived)
            .ToListAsync();
    }

    public async Task<List<AttendanceList>> GetArchivedAsync()
    {
        return await _db.AttendanceLists
            .Where(x => x.IsArchived)
            .ToListAsync();
    }

    public async Task<AttendanceList?> GetByIdAsync(int id)
    {
        return await _db.AttendanceLists
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<int> CreateAsync(AttendanceList list)
    {
        return await _db.InsertWithInt32IdentityAsync(list);
    }

    public async Task UpdateAsync(AttendanceList list)
    {
        await _db.UpdateAsync(list);
    }

    public async Task DeleteAsync(int id)
    {
        await _db.AttendanceLists
            .Where(x => x.Id == id)
            .DeleteAsync();
    }
}




