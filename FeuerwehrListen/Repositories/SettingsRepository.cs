using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;

namespace FeuerwehrListen.Repositories;

public class SettingsRepository
{
    private readonly AppDbConnection _db;

    public SettingsRepository(AppDbConnection db)
    {
        _db = db;
    }

    public async Task<List<AppSetting>> GetAllAsync()
    {
        return await _db.AppSettings.ToListAsync();
    }

    public async Task<AppSetting?> GetByKeyAsync(string key)
    {
        return await _db.AppSettings.FirstOrDefaultAsync(x => x.Key == key);
    }

    public async Task UpsertAsync(string key, string value)
    {
        var existing = await _db.AppSettings.FirstOrDefaultAsync(x => x.Key == key);
        if (existing != null)
        {
            existing.Value = value;
            await _db.UpdateAsync(existing);
        }
        else
        {
            await _db.InsertAsync(new AppSetting { Key = key, Value = value });
        }
    }
}
