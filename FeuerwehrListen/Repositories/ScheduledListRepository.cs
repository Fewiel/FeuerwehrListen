using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;

namespace FeuerwehrListen.Repositories;

public class ScheduledListRepository
{
    private readonly AppDbConnection _db;

    public ScheduledListRepository(AppDbConnection db)
    {
        _db = db;
    }

    public async Task<List<ScheduledList>> GetAllAsync()
    {
        return await _db.ScheduledLists
            .OrderBy(x => x.ScheduledEventTime)
            .ToListAsync();
    }

    public async Task<List<ScheduledList>> GetPendingAsync()
    {
        return await _db.ScheduledLists
            .Where(x => !x.IsProcessed)
            .OrderBy(x => x.ScheduledEventTime)
            .ToListAsync();
    }

    public async Task<List<ScheduledList>> GetDueAsync()
    {
        var now = DateTime.Now;
        return await _db.ScheduledLists
            .Where(x => !x.IsProcessed)
            .Where(x => x.ScheduledEventTime.AddMinutes(-x.MinutesBeforeEvent) <= now)
            .ToListAsync();
    }

    public async Task<ScheduledList?> GetByIdAsync(int id)
    {
        return await _db.ScheduledLists.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<int> CreateAsync(ScheduledList scheduledList)
    {
        return await _db.InsertWithInt32IdentityAsync(scheduledList);
    }

    public async Task UpdateAsync(ScheduledList scheduledList)
    {
        await _db.UpdateAsync(scheduledList);
    }

    public async Task DeleteAsync(int id)
    {
        await _db.ScheduledLists.Where(x => x.Id == id).DeleteAsync();
    }
}


