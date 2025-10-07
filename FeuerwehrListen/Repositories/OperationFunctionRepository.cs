using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;

namespace FeuerwehrListen.Repositories;

public class OperationFunctionRepository
{
    private readonly AppDbConnection _db;

    public OperationFunctionRepository(AppDbConnection db)
    {
        _db = db;
    }

    public Task<List<OperationFunctionDef>> GetAllAsync()
    {
        return _db.OperationFunctionDefs.OrderBy(x => x.Name).ToListAsync();
    }

    public async Task<int> CreateAsync(OperationFunctionDef def)
    {
        return await _db.InsertWithInt32IdentityAsync(def);
    }

    public async Task UpdateAsync(OperationFunctionDef def)
    {
        await _db.UpdateAsync(def);
    }

    public async Task DeleteAsync(int id)
    {
        await _db.OperationFunctionDefs.Where(x => x.Id == id).DeleteAsync();
    }
}


