using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;

namespace FeuerwehrListen.Repositories;

public class KeywordRepository
{
    private readonly AppDbConnection _connection;

    public KeywordRepository(AppDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<List<Keyword>> GetAllAsync()
    {
        return await _connection.Keywords
            .Where(k => k.IsActive)
            .OrderBy(k => k.Name)
            .ToListAsync();
    }

    public async Task<Keyword?> GetByIdAsync(int id)
    {
        return await _connection.Keywords
            .FirstOrDefaultAsync(k => k.Id == id);
    }

    public async Task<Keyword?> GetByNameAsync(string name)
    {
        return await _connection.Keywords
            .FirstOrDefaultAsync(k => k.Name == name && k.IsActive);
    }

    public async Task<int> CreateAsync(Keyword keyword)
    {
        return await _connection.InsertWithInt32IdentityAsync(keyword);
    }

    public async Task UpdateAsync(Keyword keyword)
    {
        await _connection.UpdateAsync(keyword);
    }

    public async Task DeleteAsync(int id)
    {
        var keyword = await GetByIdAsync(id);
        if (keyword != null)
        {
            keyword.IsActive = false;
            await UpdateAsync(keyword);
        }
    }

    public async Task<List<Keyword>> SearchAsync(string searchTerm, int limit = 10)
    {
        return await _connection.Keywords
            .Where(k => k.IsActive && 
                       (k.Name.Contains(searchTerm) || 
                        (k.Description != null && k.Description.Contains(searchTerm))))
            .OrderBy(k => k.Name)
            .Take(limit)
            .ToListAsync();
    }
}
