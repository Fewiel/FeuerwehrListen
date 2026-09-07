using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;

namespace FeuerwehrListen.Repositories;

public class PermissionKeyRepository
{
    private readonly AppDbConnection _db;

    public PermissionKeyRepository(AppDbConnection db)
    {
        _db = db;
    }

    public async Task<List<PermissionKey>> GetAllAsync()
    {
        return await _db.PermissionKeys.OrderBy(x => x.Name).ToListAsync();
    }

    public async Task<PermissionKey?> GetByIdAsync(int id)
    {
        return await _db.PermissionKeys.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<int> CreateAsync(PermissionKey key)
    {
        return await _db.InsertWithInt32IdentityAsync(key);
    }

    public async Task UpdateAsync(PermissionKey key)
    {
        await _db.UpdateAsync(key);
    }

    public async Task DeleteAsync(int id)
    {
        // Zuordnungen mitloeschen, damit keine verwaisten Verknuepfungen zurueckbleiben.
        await _db.UserPermissionKeys.Where(x => x.PermissionKeyId == id).DeleteAsync();
        await _db.PermissionKeys.Where(x => x.Id == id).DeleteAsync();
    }

    /// <summary>Alle Keys eines Nutzers (als Entities).</summary>
    public async Task<List<PermissionKey>> GetKeysForUserAsync(int userId)
    {
        return await (from upk in _db.UserPermissionKeys
                      join k in _db.PermissionKeys on upk.PermissionKeyId equals k.Id
                      where upk.UserId == userId
                      orderby k.Name
                      select k).ToListAsync();
    }

    /// <summary>Key-IDs eines Nutzers.</summary>
    public async Task<List<int>> GetKeyIdsForUserAsync(int userId)
    {
        return await _db.UserPermissionKeys.Where(x => x.UserId == userId)
            .Select(x => x.PermissionKeyId).ToListAsync();
    }

    /// <summary>Setzt die Key-Zuordnungen eines Nutzers exakt auf die uebergebene Liste.</summary>
    public async Task SetKeysForUserAsync(int userId, IEnumerable<int> keyIds)
    {
        var target = keyIds?.Distinct().ToList() ?? new List<int>();
        await _db.UserPermissionKeys.Where(x => x.UserId == userId).DeleteAsync();
        foreach (var keyId in target)
            await _db.InsertAsync(new UserPermissionKey { UserId = userId, PermissionKeyId = keyId });
    }

    /// <summary>Loescht alle Zuordnungen eines Nutzers (z.B. beim Loeschen des Nutzers).</summary>
    public async Task DeleteAssignmentsForUserAsync(int userId)
    {
        await _db.UserPermissionKeys.Where(x => x.UserId == userId).DeleteAsync();
    }
}
