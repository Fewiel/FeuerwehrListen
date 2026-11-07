using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace FeuerwehrListen.Services;

/// <summary>
/// Datenverzeichnis fuer Dateien ausserhalb der Datenbank (Lichtbilder). Bewusst NICHT
/// wwwroot: Lichtbilder sind personenbezogen und werden nur ueber abgesicherte Endpunkte
/// ausgeliefert. Reihenfolge: AppSettings:DataPath, sonst das Verzeichnis der SQLite-Datei
/// (im Docker-Setup das persistente Volume), sonst &lt;ContentRoot&gt;/data.
/// </summary>
public sealed class DataPathService
{
    public string Root { get; }

    public DataPathService(IConfiguration config, IWebHostEnvironment env)
    {
        var configured = config["AppSettings:DataPath"];
        string? root = string.IsNullOrWhiteSpace(configured) ? null : configured;

        if (root == null)
        {
            var cs = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
                     ?? config["DatabaseSettings:SQLiteConnection"];
            if (config["DatabaseSettings:Provider"] != "MySQL" && !string.IsNullOrWhiteSpace(cs))
            {
                foreach (var part in cs.Split(';'))
                {
                    var kv = part.Split('=', 2);
                    if (kv.Length == 2 && kv[0].Trim().Equals("Data Source", StringComparison.OrdinalIgnoreCase))
                    {
                        var dir = Path.GetDirectoryName(Path.GetFullPath(kv[1].Trim(), env.ContentRootPath));
                        if (!string.IsNullOrEmpty(dir)) root = dir;
                    }
                }
            }
        }

        Root = Path.GetFullPath(root ?? Path.Combine(env.ContentRootPath, "data"), env.ContentRootPath);
    }

    public string PhotoDirectory
    {
        get
        {
            var dir = Path.Combine(Root, "photos");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>Voller Pfad eines gespeicherten Lichtbilds; null bei ungueltigem Namen
    /// (Schutz gegen Pfad-Tricks - der Name kommt zwar aus der DB, aber sicher ist sicher).</summary>
    public string? PhotoPath(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
            return null;
        return Path.Combine(PhotoDirectory, fileName);
    }
}

/// <summary>
/// Passwort-Vergabe-Modus am Gerätehaus-Tablet. Ein angemeldeter Admin startet ihn auf einem
/// Geraet der Login-Whitelist; danach legen Mitglieder dort im Beisein des Admins ihr Passwort
/// selbst fest. Bewusst im Speicher: ein Neustart beendet den Modus (sicherer Default).
/// </summary>
public sealed class PasswordEnrollmentService
{
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    public sealed class Session
    {
        public required string AdminUsername { get; init; }
        public required int AdminUserId { get; init; }
        public required string ClientIp { get; init; }
        public DateTime StartedAt { get; init; } = DateTime.Now;
        public DateTime LastActivityAt { get; set; } = DateTime.Now;
    }

    private readonly ConcurrentDictionary<string, Session> _sessions = new(StringComparer.Ordinal);

    public Session Start(string adminUsername, int adminUserId, string clientIp)
    {
        var s = new Session { AdminUsername = adminUsername, AdminUserId = adminUserId, ClientIp = clientIp };
        _sessions[adminUsername] = s;
        return s;
    }

    public void Stop(string adminUsername) => _sessions.TryRemove(adminUsername, out _);

    /// <summary>Aktive Sitzung dieses Admins von genau diesem Geraet; abgelaufene werden entfernt.</summary>
    public Session? GetActive(string? adminUsername, string? clientIp, bool touch = false)
    {
        if (string.IsNullOrEmpty(adminUsername) || !_sessions.TryGetValue(adminUsername, out var s)) return null;
        if (DateTime.Now - s.LastActivityAt > IdleTimeout)
        {
            _sessions.TryRemove(adminUsername, out _);
            return null;
        }
        if (!string.Equals(s.ClientIp, clientIp, StringComparison.Ordinal)) return null;
        if (touch) s.LastActivityAt = DateTime.Now;
        return s;
    }
}

/// <summary>Einfaches Festfenster-Limit je Schluessel (z. B. IP) - gegen Durchprobieren von
/// Pruef-Links und Passwoertern. Reicht fuer eine Instanz; kein verteilter Zustand noetig.</summary>
public sealed class SimpleRateLimiter
{
    private readonly IMemoryCache _cache;
    public SimpleRateLimiter(IMemoryCache cache) => _cache = cache;

    private sealed class Counter { public int Count; }

    /// <summary>True = erlaubt. Zaehlt den Aufruf mit.</summary>
    public bool Allow(string key, int limit, TimeSpan window)
    {
        var c = _cache.GetOrCreate("rl:" + key, e => { e.AbsoluteExpirationRelativeToNow = window; return new Counter(); })!;
        return Interlocked.Increment(ref c.Count) <= limit;
    }
}

public static class PasswordHasher
{
    private const int Iterations = 210_000;

    /// <summary>PBKDF2-SHA256 im Format, das AuthenticationService.VerifyPassword bereits kennt.</summary>
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        return $"pbkdf2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(pbkdf2.GetBytes(32))}";
    }

    public const int MinLength = 10;
}

public static class CardIds
{
    /// <summary>128 Bit Zufall als 32 Hex-Zeichen - nicht erratbar, unabhaengig von der Mitglieds-Id.</summary>
    public static string NewPublicId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
