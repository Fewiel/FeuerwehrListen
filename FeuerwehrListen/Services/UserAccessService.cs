using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace FeuerwehrListen.Services;

/// <summary>
/// Modulrechte angemeldeter Benutzer.
///
/// Bewusst NICHT als Claim im Cookie: das Cookie lebt 30 Tage, Rechte-Aenderungen in der
/// Verwaltung wuerden sonst erst nach einem Neu-Login greifen. Stattdessen wird der
/// Benutzer per Name nachgeschlagen (kurz gecacht, beim Speichern invalidiert). Das wirkt
/// gleichermassen fuer echte Cookie-Sessions und die interne Server-Modus-Identity.
/// </summary>
public sealed class UserAccessService
{
    /// <summary>Gespeicherter Wert fuer Vollzugang (alle Module, Listen &amp; Daten).</summary>
    public const string FullAccess = "*";

    /// <summary>Brandsicherheitswachen nur ueber den Direktlink /wache/{guid} - keine Liste,
    /// keine Uebersicht, keine Wachen per Id.</summary>
    public const string ModuleFireSafetyLink = "firesafety-link";

    /// <summary>Module, die einem eingeschraenkten Benutzer einzeln gegeben werden koennen.</summary>
    public static readonly string[] SelectableModules =
    {
        NetworkAccessService.ModuleAttendance,
        NetworkAccessService.ModuleOperations,
        NetworkAccessService.ModuleFireSafety,
        ModuleFireSafetyLink,
        NetworkAccessService.ModuleCalendar,
        NetworkAccessService.ModuleDefects
    };

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _scopes;
    private readonly IMemoryCache _cache;

    public UserAccessService(IServiceScopeFactory scopes, IMemoryCache cache)
    {
        _scopes = scopes;
        _cache = cache;
    }

    /// <summary>Kurzinfo zum Benutzer; null = existiert nicht (mehr).</summary>
    public sealed record AccessInfo(bool IsAdmin, bool HasListAccess, HashSet<string>? Modules)
    {
        /// <summary>Vollzugang: Admin oder Modulrecht "*".</summary>
        public bool IsFull => IsAdmin || Modules == null;
    }

    public async Task<AccessInfo?> GetAsync(string? username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;
        var key = CacheKey(username);
        if (_cache.TryGetValue(key, out AccessInfo? cached)) return cached;

        using var scope = _scopes.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserRepository>();
        var user = await users.GetByUsernameAsync(username);
        var info = user == null ? null : new AccessInfo(user.Role == UserRole.Admin, user.HasListAccess, ParseModules(user.ModuleAccess));
        _cache.Set(key, info, CacheTtl);
        return info;
    }

    private const string ItemsKey = "fw-user-access";

    /// <summary>Laedt die Rechte des Request-Benutzers einmal je Request vor (async, in der
    /// Middleware), damit die synchrone Modulpruefung nicht blockierend auf die DB wartet.</summary>
    public async Task<AccessInfo?> PreloadAsync(HttpContext ctx)
    {
        var info = ctx.User?.Identity?.IsAuthenticated == true ? await GetAsync(ctx.User.Identity.Name) : null;
        ctx.Items[ItemsKey] = info;
        return info;
    }

    /// <summary>Rechte des Request-Benutzers; null = nicht angemeldet bzw. unbekannt.</summary>
    public AccessInfo? Get(HttpContext ctx)
    {
        if (ctx.User?.Identity?.IsAuthenticated != true) return null;
        if (ctx.Items.TryGetValue(ItemsKey, out var v)) return v as AccessInfo;
        // Fallback ohne Vorladen (sollte dank Middleware nicht vorkommen).
        return GetAsync(ctx.User.Identity.Name).GetAwaiter().GetResult();
    }

    public void Invalidate(string? username)
    {
        if (!string.IsNullOrWhiteSpace(username)) _cache.Remove(CacheKey(username));
    }

    /// <summary>null = Vollzugang, sonst die erlaubten Module.</summary>
    public static HashSet<string>? ParseModules(string? raw)
    {
        var v = (raw ?? "").Trim();
        if (v == FullAccess) return null;
        return v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .Where(x => SelectableModules.Contains(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Normalisiert die Eingabe aus der Verwaltung fuer die Datenbank.</summary>
    public static string SerializeModules(bool fullAccess, IEnumerable<string>? modules)
    {
        if (fullAccess) return FullAccess;
        var set = ParseModules(string.Join(',', modules ?? Array.Empty<string>())) ?? new HashSet<string>();
        return string.Join(',', SelectableModules.Where(set.Contains));
    }

    private static string CacheKey(string username) => "user-access:" + username;
}
