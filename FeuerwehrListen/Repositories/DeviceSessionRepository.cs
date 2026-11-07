using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;

namespace FeuerwehrListen.Repositories;

public class DeviceSessionRepository
{
    private readonly AppDbConnection _db;
    public DeviceSessionRepository(AppDbConnection db) => _db = db;

    public Task<DeviceSession?> GetByTokenHashAsync(string hash) =>
        _db.DeviceSessions.FirstOrDefaultAsync(x => x.TokenHash == hash);

    public async Task<int> InsertAsync(DeviceSession s) => s.Id = await _db.InsertWithInt32IdentityAsync(s);

    public Task TouchAsync(int id, DateTime seen, string? ip) =>
        _db.DeviceSessions.Where(x => x.Id == id).Set(x => x.LastSeenAt, seen).Set(x => x.LastIp, ip).UpdateAsync();

    public Task RevokeAllForUserAsync(int userId) =>
        _db.DeviceSessions.Where(x => x.UserId == userId && x.RevokedAt == null).Set(x => x.RevokedAt, DateTime.Now).UpdateAsync();

    public Task DeleteForUserAsync(int userId) => _db.DeviceSessions.Where(x => x.UserId == userId).DeleteAsync();

    /// <summary>Aktive Sitzungen je Geraetekonto: Anzahl, zuletzt gesehen, letzte IP.</summary>
    public async Task<Dictionary<int, (int Count, DateTime? LastSeen, string? LastIp)>> GetActiveSummaryAsync()
    {
        var rows = await _db.DeviceSessions.Where(x => x.RevokedAt == null).ToListAsync();
        return rows.GroupBy(x => x.UserId).ToDictionary(g => g.Key, g =>
        {
            var last = g.OrderByDescending(x => x.LastSeenAt).First();
            return (g.Count(), (DateTime?)last.LastSeenAt, last.LastIp);
        });
    }
}
