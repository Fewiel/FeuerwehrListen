using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;

namespace FeuerwehrListen.Repositories;

public class ApiKeyRepository
{
    private readonly AppDbConnection _db;

    public ApiKeyRepository(AppDbConnection db)
    {
        _db = db;
    }

    public async Task<List<ApiKey>> GetAllAsync()
    {
        return await _db.ApiKeys.OrderByDescending(x => x.CreatedAt).ToListAsync();
    }

    public async Task<ApiKey?> GetByIdAsync(int id)
    {
        return await _db.ApiKeys.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<ApiKey?> GetByKeyAsync(string key)
    {
        return await _db.ApiKeys.FirstOrDefaultAsync(x => x.Key == key && x.IsActive);
    }

    public async Task<bool> IsValidApiKeyAsync(string key)
    {
        var apiKey = await _db.ApiKeys.FirstOrDefaultAsync(x => x.Key == key && x.IsActive);
        return apiKey != null;
    }

    public async Task<int> CreateAsync(ApiKey apiKey)
    {
        return await _db.InsertWithInt32IdentityAsync(apiKey);
    }

    public async Task UpdateAsync(ApiKey apiKey)
    {
        await _db.UpdateAsync(apiKey);
    }

    public async Task DeleteAsync(int id)
    {
        await _db.ApiKeys.Where(x => x.Id == id).DeleteAsync();
    }
}


