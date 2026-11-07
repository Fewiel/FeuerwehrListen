using System.Security.Cryptography;
using System.Text;
using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace FeuerwehrListen.Services;

/// <summary>
/// Geraetekonten (Rolle Device): ein Geraet meldet sich einmal mit Benutzername/Passwort an
/// und traegt danach ein langlebiges Cookie (fw_device). Das Geraet gilt damit als
/// vertrauenswuerdig wie ein Geraet im Feuerwehrnetz - es ist aber NICHT "angemeldet":
/// keine verwaltenden Aktionen, nur die Kiosk-Ansicht mit den freigegebenen Bereichen.
/// Mitglieder/Admins koennen sich auf dem Geraet zusaetzlich normal anmelden.
///
/// Sperren wirkt sofort: bei jedem Aufruf wird das Token (gehasht) gegen die Sitzung
/// geprueft (60 s Cache, beim Abmelden/Passwortwechsel invalidiert).
/// </summary>
public sealed class DeviceSessionService
{
    public const string CookieName = "fw_device";
    /// <summary>Header, mit dem Server-Modus-Self-Calls das Geraete-Token weiterreichen (nur mit X-Fw-Internal).</summary>
    public const string ForwardHeader = "X-Fw-Device";
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(730);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(10);
    private const string ItemsKey = "fw-device";

    public sealed record DeviceInfo(int SessionId, int UserId, string Name, HashSet<string> Modules, bool AllowEnrollment, DateTime LastSeenAt);

    private readonly IServiceScopeFactory _scopes;
    private readonly IMemoryCache _cache;
    private readonly InternalAuthSecret _secret;

    public DeviceSessionService(IServiceScopeFactory scopes, IMemoryCache cache, InternalAuthSecret secret)
    {
        _scopes = scopes;
        _cache = cache;
        _secret = secret;
    }

    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    /// <summary>Token aus dem Request: Cookie, oder bei echten Self-Calls der Weiterreich-Header.</summary>
    public string? TokenFrom(HttpContext ctx)
    {
        if (ctx.Request.Cookies.TryGetValue(CookieName, out var c) && !string.IsNullOrWhiteSpace(c)) return c;
        var internalHeader = ctx.Request.Headers["X-Fw-Internal"].ToString();
        if (!string.IsNullOrEmpty(internalHeader)
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(internalHeader), Encoding.UTF8.GetBytes(_secret.Value)))
        {
            var h = ctx.Request.Headers[ForwardHeader].ToString();
            if (!string.IsNullOrWhiteSpace(h)) return h;
        }
        return null;
    }

    /// <summary>Einmal je Request (Middleware): Sitzung aufloesen, "zuletzt gesehen" nachziehen.</summary>
    public async Task<DeviceInfo?> PreloadAsync(HttpContext ctx, string? clientIp)
    {
        var token = TokenFrom(ctx);
        DeviceInfo? info = token == null ? null : await ResolveAsync(Hash(token), clientIp);
        ctx.Items[ItemsKey] = info;
        return info;
    }

    public DeviceInfo? Get(HttpContext ctx) => ctx.Items.TryGetValue(ItemsKey, out var v) ? v as DeviceInfo : null;

    private async Task<DeviceInfo?> ResolveAsync(string hash, string? clientIp)
    {
        var key = "device:" + hash;
        if (_cache.TryGetValue(key, out DeviceInfo? cached))
        {
            if (cached != null && DateTime.Now - cached.LastSeenAt > TouchInterval) await TouchAsync(cached, clientIp, key);
            return cached;
        }

        using var scope = _scopes.CreateScope();
        var sessions = scope.ServiceProvider.GetRequiredService<DeviceSessionRepository>();
        var users = scope.ServiceProvider.GetRequiredService<UserRepository>();
        var s = await sessions.GetByTokenHashAsync(hash);
        DeviceInfo? info = null;
        if (s != null && s.RevokedAt == null && await users.GetByIdAsync(s.UserId) is User u && u.Role == UserRole.Device)
        {
            info = new DeviceInfo(s.Id, u.Id, DisplayName(u), UserAccessService.ParseModules(u.ModuleAccess) ?? new HashSet<string>(),
                u.AllowEnrollment, s.LastSeenAt);
            if (DateTime.Now - s.LastSeenAt > TouchInterval)
            {
                await sessions.TouchAsync(s.Id, DateTime.Now, clientIp);
                info = info with { LastSeenAt = DateTime.Now };
            }
        }
        _cache.Set(key, info, CacheTtl);
        return info;
    }

    private async Task TouchAsync(DeviceInfo info, string? clientIp, string key)
    {
        using var scope = _scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DeviceSessionRepository>().TouchAsync(info.SessionId, DateTime.Now, clientIp);
        _cache.Set(key, info with { LastSeenAt = DateTime.Now }, CacheTtl);
    }

    /// <summary>Neue Geraete-Sitzung anlegen und Cookie setzen (nach Login eines Geraetekontos).</summary>
    public async Task SignInAsync(HttpContext ctx, User user, string? clientIp)
    {
        var token = NewToken();
        using (var scope = _scopes.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DeviceSessionRepository>().InsertAsync(new DeviceSession
            {
                UserId = user.Id,
                TokenHash = Hash(token),
                UserAgent = Truncate(ctx.Request.Headers.UserAgent.ToString(), 300),
                CreatedAt = DateTime.Now,
                LastSeenAt = DateTime.Now,
                LastIp = clientIp
            });
        }
        ctx.Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.Now.Add(Lifetime),
            Path = "/",
            IsEssential = true
        });
    }

    /// <summary>Alle Geraete dieses Kontos abmelden (Sperre, Passwortwechsel, Loeschen).</summary>
    public async Task RevokeAllAsync(int userId)
    {
        using var scope = _scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DeviceSessionRepository>().RevokeAllForUserAsync(userId);
        // Cache-Eintraege sind je Token-Hash - einfach alle Geraete-Eintraege verwerfen.
        if (_cache is MemoryCache mc) mc.Compact(1.0);
    }

    public static string DisplayName(User u) =>
        string.IsNullOrWhiteSpace(u.FirstName + u.LastName) ? u.Username : $"{u.FirstName} {u.LastName}".Trim();

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
