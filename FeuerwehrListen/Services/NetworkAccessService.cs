using System.Net;
using System.Net.Sockets;
using FeuerwehrListen.Models;

namespace FeuerwehrListen.Services;

/// <summary>
/// Entscheidet, welche Module ein Request sehen darf.
///
/// Zwei unabhaengige Einschraenkungen greifen nacheinander:
/// 1. HOST-PROFIL - je Hostname konfigurierbar, welche Module sichtbar sind
///    (z. B. wachen.example.de zeigt nur Brandsicherheitswachen).
/// 2. NETZ-SCHRANKE - ausserhalb der vertrauenswuerdigen Netze sehen NICHT angemeldete
///    Besucher nur die ausdruecklich freigegebenen Module.
///
/// Angemeldete Benutzer umgehen die Netz-Schranke immer (sonst koennte man von unterwegs
/// nichts mehr nachtragen). Ob sie auch Host-Profile umgehen, ist einstellbar.
/// </summary>
public sealed class NetworkAccessService
{
    private readonly SettingsService _settings;
    private readonly InternalAuthSecret _secret;

    public NetworkAccessService(SettingsService settings, InternalAuthSecret secret)
    {
        _settings = settings;
        _secret = secret;
    }

    /// <summary>
    /// Der Host, den der Nutzer tatsaechlich aufgerufen hat.
    ///
    /// Im Blazor-Server-Modus (Alt-Geraete) ruft der Server seine eigenen Endpunkte auf.
    /// Der Host-Header dieses Self-Calls ist dann eine technische Zieladresse und nicht
    /// die Adresse des Nutzers. Der echte Host kommt deshalb als X-Fw-Host mit - dem
    /// wird nur geglaubt, wenn der Aufruf das prozess-geheime X-Fw-Internal traegt.
    /// </summary>
    public string GetEffectiveHost(HttpContext ctx)
    {
        var internalHeader = ctx.Request.Headers["X-Fw-Internal"].ToString();
        if (!string.IsNullOrEmpty(internalHeader) && internalHeader == _secret.Value)
        {
            var forwarded = ctx.Request.Headers["X-Fw-Host"].ToString();
            if (!string.IsNullOrWhiteSpace(forwarded)) return forwarded;
        }
        return ctx.Request.Host.Value ?? string.Empty;
    }

    public const string ModuleAttendance = "attendance";
    public const string ModuleOperations = "operations";
    public const string ModuleFireSafety = "firesafety";
    public const string ModuleDefects = "defects";
    public const string ModuleCalendar = "calendar";

    private static readonly string[] AllModules =
        { ModuleAttendance, ModuleOperations, ModuleFireSafety, ModuleDefects, ModuleCalendar };

    /// <summary>
    /// Sonderprofil "approve": auf diesem Host ist NUR der Freigabe-Weg offen.
    /// Gedacht fuer eine Subdomain ohne Passwortschutz, deren Link per Mail nach
    /// draussen geht. Ein leeres Modulprofil reicht dafuer NICHT - Endpunkte wie
    /// die Mitgliedersuche oder die Anmeldung sind bewusst nicht modulgebunden
    /// und blieben sonst erreichbar.
    /// </summary>
    public const string ProfileApproveOnly = "approve";

    // ------------------------------------------------------------------ IP

    /// <summary>
    /// Echte Client-IP. X-Forwarded-For wird NUR ausgewertet, wenn der direkte Absender ein
    /// hinterlegter Proxy ist - der Header ist sonst frei faelschbar und die ganze
    /// Netz-Schranke waere mit einer Zeile umgehbar.
    /// </summary>
    public IPAddress? GetClientIp(HttpContext ctx)
    {
        var remote = Normalize(ctx.Connection.RemoteIpAddress);
        if (remote == null) return null;

        var trustedProxies = ParseList(_settings.GetSetting(SettingKeys.SecurityTrustedProxies));
        if (trustedProxies.Count == 0) return remote;
        if (!IsInAny(remote, trustedProxies)) return remote;

        var xff = ctx.Request.Headers["X-Forwarded-For"].ToString();
        if (string.IsNullOrWhiteSpace(xff)) return remote;

        // Format: "client, proxy1, proxy2" - von rechts nach links laufen und bekannte
        // Proxies ueberspringen; der erste Fremde ist der echte Client.
        var parts = xff.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = parts.Length - 1; i >= 0; i--)
        {
            var candidate = Normalize(ParseIp(parts[i]));
            if (candidate == null) continue;
            if (IsInAny(candidate, trustedProxies)) continue;
            return candidate;
        }
        return remote;
    }

    public bool IsTrustedNetwork(HttpContext ctx)
    {
        var ip = GetClientIp(ctx);
        if (ip == null) return false;
        var networks = ParseList(_settings.GetSetting(SettingKeys.SecurityTrustedNetworks));
        if (networks.Count == 0) networks = ParseList(DefaultTrustedNetworks);
        return IsInAny(ip, networks);
    }

    public const string DefaultTrustedNetworks = "127.0.0.1/32,::1/128,10.0.0.0/8,172.16.0.0/12,192.168.0.0/16,169.254.0.0/16,fc00::/7";
    public const string DefaultExternalModules = ModuleCalendar + "," + ModuleFireSafety;

    // -------------------------------------------------------------- Module

    /// <summary>Global eingeschaltete Module (Modul-Sichtbarkeit in den Einstellungen).</summary>
    private HashSet<string> GloballyEnabled()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_settings.IsModuleVisible(SettingKeys.VisibilityAttendance)) set.Add(ModuleAttendance);
        if (_settings.IsModuleVisible(SettingKeys.VisibilityOperations)) set.Add(ModuleOperations);
        if (_settings.IsModuleVisible(SettingKeys.VisibilityFireSafetyWatch)) set.Add(ModuleFireSafety);
        if (_settings.IsModuleVisible(SettingKeys.VisibilityDefects)) set.Add(ModuleDefects);
        if (_settings.IsModuleVisible(SettingKeys.VisibilityCalendar)) set.Add(ModuleCalendar);
        return set;
    }

    /// <summary>
    /// Host-Profile aus den Einstellungen. Ein Eintrag je Zeile: "host = modul1, modul2".
    /// Gibt null zurueck, wenn fuer diesen Host kein Profil hinterlegt ist.
    /// </summary>
    public HashSet<string>? GetHostProfile(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return null;
        var raw = _settings.GetSetting(SettingKeys.SecurityHostProfiles);
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // Port abschneiden, damit "wachen.example.de:8080" ebenfalls trifft.
        var h = host.Trim().ToLowerInvariant();
        var colon = h.LastIndexOf(':');
        if (colon > 0 && !h.Contains(']')) h = h[..colon];

        foreach (var line in raw.Split(new[] { '\n', '\r', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var profileHost = line[..eq].Trim().ToLowerInvariant();
            if (profileHost != h) continue;

            var mods = line[(eq + 1)..]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.ToLowerInvariant())
                .Where(x => AllModules.Contains(x) || x == ProfileApproveOnly)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return mods;
        }
        return null;
    }

    /// <summary>
    /// True, wenn fuer diesen Host nur der Freigabe-Weg offen ist.
    /// Gilt bewusst AUCH fuer angemeldete Benutzer und unabhaengig von
    /// HostProfilesApplyToLoggedIn: die Subdomain ist oeffentlich erreichbar,
    /// da darf es keinen Weg an der Beschraenkung vorbei geben.
    /// </summary>
    public bool IsApproveOnlyHost(HttpContext ctx)
    {
        var profile = GetHostProfile(GetEffectiveHost(ctx));
        return profile != null && profile.Contains(ProfileApproveOnly);
    }

    /// <summary>Die einzigen Endpunkte, die ein "approve"-Host erreichen darf:
    /// der Freigabe-Vorgang selbst und der App-Kontext fuer die Kopfzeile.</summary>
    public static bool IsApproveOnlyAllowedPath(PathString path)
    {
        if (!path.HasValue) return false;
        var p = path.Value!.ToLowerInvariant();
        return p.StartsWith("/client-api/approve") || p.StartsWith("/client-api/app-context");
    }

    /// <summary>Module, die dieser Request tatsaechlich nutzen darf.</summary>
    public HashSet<string> GetAllowedModules(HttpContext ctx)
    {
        var authenticated = ctx.User.Identity?.IsAuthenticated ?? false;
        var allowed = GloballyEnabled();

        // 1. Host-Profil
        var profile = GetHostProfile(GetEffectiveHost(ctx));
        if (profile != null)
        {
            var applyToLoggedIn = IsTrue(_settings.GetSetting(SettingKeys.SecurityHostProfilesApplyToLoggedIn));
            if (!authenticated || applyToLoggedIn)
                allowed.IntersectWith(profile);
        }

        // 2. Netz-Schranke (Angemeldete ausgenommen)
        if (!authenticated && IsTrue(_settings.GetSetting(SettingKeys.SecurityRestrictExternal)) && !IsTrustedNetwork(ctx))
        {
            var raw = _settings.GetSetting(SettingKeys.SecurityExternalModules);
            var external = (string.IsNullOrWhiteSpace(raw) ? DefaultExternalModules : raw)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.ToLowerInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            allowed.IntersectWith(external);
        }

        return allowed;
    }

    /// <summary>
    /// Modul eines Endpoints. null = nicht modulgebunden (Anmeldung, App-Kontext,
    /// Freigabe-Link, Admin-Bereich) und damit nie durch diese Schranke blockiert.
    /// </summary>
    public static string? ModuleForPath(PathString path)
    {
        if (!path.HasValue) return null;
        var p = path.Value!.ToLowerInvariant();

        // Freigabe-Link ist bewusst IMMER erreichbar - er geht per Mail nach draussen.
        if (p.StartsWith("/client-api/approve")) return null;
        // Admin- und Login-Wege haben eigene Absicherung.
        if (p.StartsWith("/client-api/admin") || p.StartsWith("/client-api/auth")) return null;
        if (p.StartsWith("/client-api/app-context")) return null;
        // Mitgliedersuche wird auch beim Buchen/Wachen-Eintragen gebraucht.
        if (p.StartsWith("/client-api/members/search")) return null;
        // Uebersichts-Endpoints filtern selbst nach erlaubten Modulen.
        if (p.StartsWith("/client-api/open-lists")) return null;

        if (p.StartsWith("/client-api/attendance") || p.StartsWith("/client-api/export/attendance")) return ModuleAttendance;
        if (p.StartsWith("/client-api/operation") || p.StartsWith("/client-api/export/operation")
            || p.StartsWith("/client-api/keywords") || p.StartsWith("/client-api/feedback")) return ModuleOperations;
        if (p.StartsWith("/client-api/firesafetywatch")) return ModuleFireSafety;
        if (p.StartsWith("/client-api/calendar")) return ModuleCalendar;
        if (p.StartsWith("/client-api/defects") || p.StartsWith("/client-api/vehicles-active")) return ModuleDefects;

        return null;
    }

    // ------------------------------------------------------------ Helfer

    private static bool IsTrue(string? v) => v != null && v.Equals("true", StringComparison.OrdinalIgnoreCase);

    private static IPAddress? Normalize(IPAddress? ip)
    {
        if (ip == null) return null;
        // ::ffff:192.168.1.5 auf die echte IPv4 zurueckfuehren.
        return ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;
    }

    private static IPAddress? ParseIp(string s)
    {
        s = s.Trim();
        // "[::1]:1234" bzw. "1.2.3.4:1234"
        if (s.StartsWith('['))
        {
            var end = s.IndexOf(']');
            if (end > 0) s = s[1..end];
        }
        else if (s.Count(c => c == ':') == 1)
        {
            s = s[..s.IndexOf(':')];
        }
        return IPAddress.TryParse(s, out var ip) ? ip : null;
    }

    private static List<(IPAddress Network, int Prefix)> ParseList(string? raw)
    {
        var result = new List<(IPAddress, int)>();
        if (string.IsNullOrWhiteSpace(raw)) return result;
        foreach (var entry in raw.Split(new[] { ',', ';', '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var e = entry.Trim();
            if (e.Length == 0) continue;
            var slash = e.IndexOf('/');
            var addrPart = slash >= 0 ? e[..slash] : e;
            if (!IPAddress.TryParse(addrPart, out var addr)) continue;
            addr = Normalize(addr)!;
            var maxPrefix = addr.AddressFamily == AddressFamily.InterNetworkV6 ? 128 : 32;
            var prefix = maxPrefix;
            if (slash >= 0 && int.TryParse(e[(slash + 1)..], out var p) && p >= 0 && p <= maxPrefix)
                prefix = p;
            result.Add((addr, prefix));
        }
        return result;
    }

    private static bool IsInAny(IPAddress ip, List<(IPAddress Network, int Prefix)> networks)
    {
        foreach (var (network, prefix) in networks)
        {
            if (IsInNetwork(ip, network, prefix)) return true;
        }
        return false;
    }

    /// <summary>Bitweiser Praefix-Vergleich - bewusst selbst gebaut statt IPNetwork,
    /// damit auch nicht normalisierte Eingaben (z. B. 192.168.1.0/16) tolerant behandelt werden.</summary>
    private static bool IsInNetwork(IPAddress ip, IPAddress network, int prefix)
    {
        if (ip.AddressFamily != network.AddressFamily) return false;
        var a = ip.GetAddressBytes();
        var b = network.GetAddressBytes();
        if (a.Length != b.Length) return false;

        var fullBytes = prefix / 8;
        var restBits = prefix % 8;

        for (var i = 0; i < fullBytes; i++)
            if (a[i] != b[i]) return false;

        if (restBits == 0) return true;
        var mask = (byte)(0xFF << (8 - restBits));
        return (a[fullBytes] & mask) == (b[fullBytes] & mask);
    }
}
