using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;

namespace FeuerwehrListen.Repositories;

public class OperationListRepository
{
    private readonly AppDbConnection _db;

    public OperationListRepository(AppDbConnection db)
    {
        _db = db;
    }

    public async Task<List<OperationList>> GetAllAsync()
    {
        return await _db.OperationLists.ToListAsync();
    }

    public async Task<List<OperationList>> GetOpenAsync()
    {
        return await _db.OperationLists
            .Where(x => x.Status == ListStatus.Open && !x.IsArchived)
            .ToListAsync();
    }

    public async Task<List<OperationList>> GetClosedAsync()
    {
        return await _db.OperationLists
            .Where(x => x.Status == ListStatus.Closed && !x.IsArchived)
            .ToListAsync();
    }

    public async Task<List<OperationList>> GetArchivedAsync()
    {
        return await _db.OperationLists
            .Where(x => x.IsArchived)
            .ToListAsync();
    }

    public async Task<OperationList?> GetByIdAsync(int id)
    {
        return await _db.OperationLists
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<int> CreateAsync(OperationList list)
    {
        return await _db.InsertWithInt32IdentityAsync(list);
    }

    public async Task UpdateAsync(OperationList list)
    {
        await _db.UpdateAsync(list);
    }

    public async Task DeleteAsync(int id)
    {
        await _db.OperationLists
            .Where(x => x.Id == id)
            .DeleteAsync();
    }
}




