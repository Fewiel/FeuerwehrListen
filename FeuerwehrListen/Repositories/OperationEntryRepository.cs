using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;

namespace FeuerwehrListen.Repositories;

public class OperationEntryRepository
{
    private readonly AppDbConnection _db;

    public OperationEntryRepository(AppDbConnection db)
    {
        _db = db;
    }

    public async Task<List<OperationEntry>> GetByListIdAsync(int listId)
    {
        return await _db.OperationEntries
            .Where(x => x.OperationListId == listId)
            .OrderBy(x => x.EnteredAt)
            .ToListAsync();
    }

    public async Task<int> CreateAsync(OperationEntry entry)
    {
        return await _db.InsertWithInt32IdentityAsync(entry);
    }

    public async Task DeleteAsync(int id)
    {
        await _db.OperationEntries
            .Where(x => x.Id == id)
            .DeleteAsync();
    }
}


