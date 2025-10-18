using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;

namespace FeuerwehrListen.Repositories;

public class AttendanceEntryRepository
{
    private readonly AppDbConnection _db;

    public AttendanceEntryRepository(AppDbConnection db)
    {
        _db = db;
    }

    public async Task<List<AttendanceEntry>> GetByListIdAsync(int listId)
    {
        return await _db.AttendanceEntries
            .Where(x => x.AttendanceListId == listId)
            .OrderBy(x => x.EnteredAt)
            .ToListAsync();
    }

    public async Task<int> CreateAsync(AttendanceEntry entry)
    {
        return await _db.InsertWithInt32IdentityAsync(entry);
    }

    public async Task DeleteAsync(int id)
    {
        await _db.AttendanceEntries
            .Where(x => x.Id == id)
            .DeleteAsync();
    }
}




