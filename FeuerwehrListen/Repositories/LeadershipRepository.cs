using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;

namespace FeuerwehrListen.Repositories;

/// <summary>Datenzugriff Fuehrungskreis: Protokolle, Tagesordnungspunkte, Aufgaben.</summary>
public class LeadershipRepository
{
    private readonly AppDbConnection _db;

    public LeadershipRepository(AppDbConnection db) => _db = db;

    // --- Protokolle ---
    public Task<LeadershipProtocol?> GetProtocolAsync(int id) =>
        _db.LeadershipProtocols.FirstOrDefaultAsync(x => x.Id == id);

    public Task<LeadershipProtocol?> GetProtocolByEventAsync(int eventId) =>
        _db.LeadershipProtocols.FirstOrDefaultAsync(x => x.CalendarEventId == eventId);

    public Task<List<LeadershipProtocol>> GetProtocolsAsync() => _db.LeadershipProtocols.ToListAsync();

    public async Task<int> InsertProtocolAsync(LeadershipProtocol p) => p.Id = await _db.InsertWithInt32IdentityAsync(p);
    public Task UpdateProtocolAsync(LeadershipProtocol p) => _db.UpdateAsync(p);

    // --- Tagesordnungspunkte ---
    public Task<List<LeadershipItem>> GetItemsAsync(int protocolId) =>
        _db.LeadershipItems.Where(x => x.ProtocolId == protocolId).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync();

    /// <summary>Punkte, die auf die naechste Sitzung dieses Kreises warten.</summary>
    public Task<List<LeadershipItem>> GetBacklogAsync(int circle) =>
        _db.LeadershipItems.Where(x => x.ProtocolId == null && x.Circle == circle).OrderBy(x => x.CreatedAt).ToListAsync();

    public Task<LeadershipItem?> GetItemAsync(int id) => _db.LeadershipItems.FirstOrDefaultAsync(x => x.Id == id);

    public Task<List<LeadershipItem>> GetItemsBySourceTaskAsync(int taskId) =>
        _db.LeadershipItems.Where(x => x.SourceTaskId == taskId).ToListAsync();

    public Task<List<LeadershipItem>> GetItemsByIdsAsync(ICollection<int> ids) =>
        ids.Count == 0 ? Task.FromResult(new List<LeadershipItem>()) : _db.LeadershipItems.Where(x => ids.Contains(x.Id)).ToListAsync();

    public async Task<int> InsertItemAsync(LeadershipItem i) => i.Id = await _db.InsertWithInt32IdentityAsync(i);
    public Task UpdateItemAsync(LeadershipItem i) => _db.UpdateAsync(i);
    public Task DeleteItemAsync(int id) => _db.LeadershipItems.Where(x => x.Id == id).DeleteAsync();

    public async Task<int> NextSortOrderAsync(int protocolId)
    {
        var max = await _db.LeadershipItems.Where(x => x.ProtocolId == protocolId).Select(x => (int?)x.SortOrder).MaxAsync();
        return (max ?? 0) + 10;
    }

    // --- Aufgaben ---
    public Task<List<LeadershipTask>> GetTasksAsync(LeadershipTaskStatus? status = null) =>
        (status is LeadershipTaskStatus st ? _db.LeadershipTasks.Where(x => x.Status == st) : _db.LeadershipTasks)
            .OrderBy(x => x.DueDate == null ? 1 : 0).ThenBy(x => x.DueDate).ThenBy(x => x.Id).ToListAsync();

    public Task<List<LeadershipTask>> GetTasksForItemsAsync(ICollection<int> itemIds) =>
        itemIds.Count == 0 ? Task.FromResult(new List<LeadershipTask>())
            : _db.LeadershipTasks.Where(x => x.SourceItemId != null && itemIds.Contains(x.SourceItemId.Value)).ToListAsync();

    public Task<LeadershipTask?> GetTaskAsync(int id) => _db.LeadershipTasks.FirstOrDefaultAsync(x => x.Id == id);
    public async Task<int> InsertTaskAsync(LeadershipTask t) => t.Id = await _db.InsertWithInt32IdentityAsync(t);
    public Task UpdateTaskAsync(LeadershipTask t) => _db.UpdateAsync(t);
    public Task DeleteTaskAsync(int id) => _db.LeadershipTasks.Where(x => x.Id == id).DeleteAsync();
}
