using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;

namespace FeuerwehrListen.Repositories;

public class SsoClientRepository
{
    private readonly AppDbConnection _db;

    public SsoClientRepository(AppDbConnection db)
    {
        _db = db;
    }

    public async Task<List<SsoClient>> GetAllAsync()
    {
        return await _db.SsoClients.OrderBy(x => x.Name).ToListAsync();
    }

    public async Task<SsoClient?> GetByIdAsync(int id)
    {
        return await _db.SsoClients.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<SsoClient?> GetByClientIdAsync(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId)) return null;
        return await _db.SsoClients.FirstOrDefaultAsync(x => x.ClientId == clientId);
    }

    public async Task<int> CreateAsync(SsoClient client)
    {
        return await _db.InsertWithInt32IdentityAsync(client);
    }

    public async Task UpdateAsync(SsoClient client)
    {
        await _db.UpdateAsync(client);
    }

    public async Task DeleteAsync(int id)
    {
        await _db.SsoClients.Where(x => x.Id == id).DeleteAsync();
    }
}
