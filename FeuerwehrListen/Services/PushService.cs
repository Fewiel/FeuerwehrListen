using System.Text.Json;
using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;
using WebPush;

namespace FeuerwehrListen.Services;

/// <summary>
/// Web-Push an Mitglieder (moderne Browser / installierte Web-App). VAPID-Schluessel werden beim
/// ersten Bedarf erzeugt und in den Einstellungen gespeichert. Abgelaufene Abos (404/410) werden
/// entfernt. Fehler beim Senden brechen nie den ausloesenden Vorgang ab.
/// </summary>
public class PushService
{
    private const string PublicKeySetting = "Push.VapidPublicKey", PrivateKeySetting = "Push.VapidPrivateKey";
    private static readonly SemaphoreSlim KeyLock = new(1, 1);

    private readonly AppDbConnection _db;
    private readonly SettingsService _settings;
    private readonly AppUrlService _urls;
    private readonly ILogger<PushService> _logger;

    public PushService(AppDbConnection db, SettingsService settings, AppUrlService urls, ILogger<PushService> logger)
    {
        _db = db; _settings = settings; _urls = urls; _logger = logger;
    }

    public async Task<VapidDetails> GetVapidAsync()
    {
        var pub = _settings.GetSetting(PublicKeySetting); var priv = _settings.GetSetting(PrivateKeySetting);
        if (string.IsNullOrWhiteSpace(pub) || string.IsNullOrWhiteSpace(priv))
        {
            await KeyLock.WaitAsync();
            try
            {
                pub = _settings.GetSetting(PublicKeySetting); priv = _settings.GetSetting(PrivateKeySetting);
                if (string.IsNullOrWhiteSpace(pub) || string.IsNullOrWhiteSpace(priv))
                {
                    var keys = VapidHelper.GenerateVapidKeys();
                    await _settings.UpdateSettingAsync(PublicKeySetting, keys.PublicKey);
                    await _settings.UpdateSettingAsync(PrivateKeySetting, keys.PrivateKey);
                    pub = keys.PublicKey; priv = keys.PrivateKey;
                }
            }
            finally { KeyLock.Release(); }
        }
        var subject = _urls.GetBaseUrl() is { Length: > 0 } b && b.StartsWith("https") ? b : "mailto:admin@feuerwehr.local";
        return new VapidDetails(subject, pub!, priv!);
    }

    public async Task SubscribeAsync(int memberId, string endpoint, string p256dh, string auth)
    {
        await _db.GetTable<MemberPushSubscription>().Where(s => s.Endpoint == endpoint).DeleteAsync();
        await _db.InsertAsync(new MemberPushSubscription { MemberId = memberId, Endpoint = endpoint, P256dh = p256dh, Auth = auth, CreatedAt = DateTime.Now });
    }

    public Task UnsubscribeAsync(string endpoint) => _db.GetTable<MemberPushSubscription>().Where(s => s.Endpoint == endpoint).DeleteAsync();

    public Task<bool> HasSubscriptionAsync(int memberId) => _db.GetTable<MemberPushSubscription>().AnyAsync(s => s.MemberId == memberId);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, DateTime> RemindedEvents = new();

    /// <summary>Erinnerung ca. 1 Stunde vor Beginn eines Dienstabends an Mitglieder der Einheit
    /// (ohne Einheit: alle) mit Push-Abo - ausser sie haben sich fuer den Zeitraum abgemeldet.</summary>
    public async Task RemindUpcomingDutiesAsync(DateTime now)
    {
        var events = await _db.CalendarEvents
            .Where(e => e.Type == CalendarEventType.Dienst && e.Status != CalendarEventStatus.Storniert && e.Status != CalendarEventStatus.Abgelehnt
                        && e.StartTime > now.AddMinutes(45) && e.StartTime <= now.AddMinutes(60))
            .ToListAsync();
        foreach (var e in events)
        {
            if (!RemindedEvents.TryAdd(e.Id, now)) continue;
            var memberIds = await _db.GetTable<MemberPushSubscription>().Select(s => s.MemberId).Distinct().ToListAsync();
            foreach (var mid in memberIds)
            {
                var m = await _db.Members.FirstOrDefaultAsync(x => x.Id == mid && x.IsActive);
                if (m == null) continue;
                if (e.UnitNumber is int unit)
                {
                    var units = (await _db.MemberUnits.Where(x => x.MemberId == m.Id).Select(x => x.UnitNumber).ToListAsync()).ToHashSet();
                    if (m.UnitNumber is int pu) units.Add(pu);
                    if (units.Count == 0 && int.TryParse(m.MemberNumber, out var n) && n > 999) units.Add(n / 1000);
                    if (!units.Contains(unit)) continue;
                }
                if (await _db.MemberAbsences.AnyAsync(a => a.MemberId == m.Id && a.From < e.EndTime && a.To > e.StartTime)) continue;
                await SendToMemberAsync(m.Id, $"Gleich: {e.Title}", $"Beginn {e.StartTime:HH:mm} Uhr{(string.IsNullOrWhiteSpace(e.Location) ? "" : " · " + e.Location)}", "/mein");
            }
        }
        foreach (var old in RemindedEvents.Where(kv => kv.Value < now.AddDays(-1)).Select(kv => kv.Key).ToList()) RemindedEvents.TryRemove(old, out _);
    }

    /// <summary>Benachrichtigung an alle Geraete eines Mitglieds. url = Seite, die beim Antippen oeffnet.</summary>
    public async Task SendToMemberAsync(int memberId, string title, string body, string url)
    {
        try
        {
            var subs = await _db.GetTable<MemberPushSubscription>().Where(s => s.MemberId == memberId).ToListAsync();
            if (subs.Count == 0) return;
            var vapid = await GetVapidAsync();
            var payload = JsonSerializer.Serialize(new { title, body, url });
            using var client = new WebPushClient();
            foreach (var s in subs)
            {
                try { await client.SendNotificationAsync(new PushSubscription(s.Endpoint, s.P256dh, s.Auth), payload, vapid); }
                catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound)
                {
                    await _db.GetTable<MemberPushSubscription>().Where(x => x.Id == s.Id).DeleteAsync();
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Push an Mitglied {Member} fehlgeschlagen.", memberId); }
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Push-Versand fehlgeschlagen."); }
    }
}
