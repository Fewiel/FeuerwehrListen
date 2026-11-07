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

/// <summary>
/// Login-Bremse gegen Durchprobieren (UI und API). Zaehlt FEHLversuche je Konto und je
/// Client-IP in einem 15-Minuten-Fenster:
/// * jeder Fehlversuch wird kuenstlich verzoegert - laenger, je mehr Fehlversuche (bis 4 s),
/// * ab 10 Fehlversuchen auf ein Konto ist es 15 Minuten gesperrt (auch fuer den Richtigen),
/// * ab 50 Fehlversuchen von einer IP ist die IP 15 Minuten gesperrt.
/// Erfolgreiche Anmeldung setzt den Konto-Zaehler zurueck. Im Speicher - ein Neustart loescht
/// die Zaehler, was fuer eine Bremse ausreicht (Mitglieder haben zusaetzlich die DB-Sperre).
/// </summary>
public sealed class LoginThrottle
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    public const int AccountLimit = 10;
    public const int IpLimit = 50;
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(4);

    private readonly IMemoryCache _cache;
    public LoginThrottle(IMemoryCache cache) => _cache = cache;

    private sealed class Counter { public int Count; public DateTime LockedUntil; }

    private Counter Get(string key) => _cache.GetOrCreate("lt:" + key, e => { e.SlidingExpiration = Window; return new Counter(); })!;

    private static string AcctKey(string? account) => "acct:" + (account ?? "").Trim().ToLowerInvariant();
    private static string IpKey(string? ip) => "ip:" + (ip ?? "?");

    /// <summary>Gesperrt? Liefert das Ende der Sperre fuer Konto oder IP.</summary>
    public DateTime? LockedUntil(string? account, string? ip)
    {
        var now = DateTime.Now;
        DateTime? until = null;
        foreach (var key in new[] { IpKey(ip), AcctKey(account) })
        {
            if (_cache.TryGetValue("lt:" + key, out Counter? c) && c != null && c.LockedUntil > now)
                until = until == null || c.LockedUntil > until ? c.LockedUntil : until;
        }
        return until;
    }

    /// <summary>Fehlversuch zaehlen; danach kuenstlich warten (Bremse). Rueckgabe: Sperre, falls jetzt erreicht.</summary>
    public async Task<DateTime?> FailAsync(string? account, string? ip)
    {
        var now = DateTime.Now;
        var a = Get(AcctKey(account)); var i = Get(IpKey(ip));
        a.Count++; i.Count++;
        if (a.Count >= AccountLimit && a.LockedUntil <= now) a.LockedUntil = now.Add(Window);
        if (i.Count >= IpLimit && i.LockedUntil <= now) i.LockedUntil = now.Add(Window);
        // Jeder Fehlversuch kostet Zeit: 300 ms je Konto-Fehlversuch, dazu etwas je IP-Fehlversuch.
        var delay = TimeSpan.FromMilliseconds(Math.Min(MaxDelay.TotalMilliseconds, 300 * a.Count + 50 * i.Count));
        await Task.Delay(delay);
        return LockedUntil(account, ip);
    }

    public void Success(string? account) => _cache.Remove("lt:" + AcctKey(account));

    /// <summary>Konto-Sperre aufheben (Verwaltung).</summary>
    public void Unlock(string? account) => Success(account);
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

    /// <summary>Fuer unbekannte Konten wird gegen diesen Hash geprueft, damit die Antwortzeit nicht
    /// verraet, ob ein Benutzername existiert.</summary>
    public static readonly string DummyHash = Hash(Guid.NewGuid().ToString());
}

public static class CardIds
{
    /// <summary>128 Bit Zufall als 32 Hex-Zeichen - nicht erratbar, unabhaengig von der Mitglieds-Id.</summary>
    public static string NewPublicId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
