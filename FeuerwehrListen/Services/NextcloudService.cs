using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using FeuerwehrListen.Models;

namespace FeuerwehrListen.Services;

/// <summary>
/// Lädt Einsatzbilder per WebDAV nach Nextcloud hoch und legt dabei automatisch den
/// Ordner {Basis}/{JJJJ}/{JJ}{Einsatznummer} an. Auth per Basis-Auth mit App-Passwort.
/// </summary>
public class NextcloudService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly SettingsService _settings;

    public NextcloudService(IHttpClientFactory httpFactory, SettingsService settings)
    {
        _httpFactory = httpFactory;
        _settings = settings;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_settings.GetSetting(SettingKeys.NextcloudUrl)) &&
        !string.IsNullOrWhiteSpace(_settings.GetSetting(SettingKeys.NextcloudUsername)) &&
        !string.IsNullOrWhiteSpace(_settings.GetSetting(SettingKeys.NextcloudAppPassword));

    public string BasePath => _settings.GetSetting(SettingKeys.NextcloudBasePath) ?? "/Feuerwehr Billerbeck/Einsatzbilder";

    private (string url, string user, string pass, string basePath) Config()
        => (_settings.GetSetting(SettingKeys.NextcloudUrl) ?? "",
            _settings.GetSetting(SettingKeys.NextcloudUsername) ?? "",
            _settings.GetSetting(SettingKeys.NextcloudAppPassword) ?? "",
            BasePath);

    private HttpClient CreateClient(string user, string pass)
    {
        var client = _httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        return client;
    }

    private static string DavRoot(string baseUrl, string user)
        => $"{baseUrl.TrimEnd('/')}/remote.php/dav/files/{Uri.EscapeDataString(user)}";

    private static string EncodePath(string relative)
        => string.Join("/", relative.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));

    /// <summary>Baut den relativen Ordnerpfad: {Basis}/{JJJJ}/{JJ}{Einsatznummer}.</summary>
    public string BuildOperationFolder(int year, string operationNumber)
    {
        var basePath = BasePath.Trim('/');
        var yy = (year % 100).ToString("D2");
        var num = StripLeadingYear(operationNumber, year);
        return $"{basePath}/{year}/{yy}{num}";
    }

    /// <summary>
    /// Entfernt ein führendes volles Jahr (optional mit „-") aus der Einsatznummer,
    /// z. B. „2026-000999" -> „000999". Nummern ohne Jahr bleiben unverändert.
    /// </summary>
    public static string StripLeadingYear(string? operationNumber, int year)
    {
        var num = (operationNumber ?? "").Trim();
        var m = System.Text.RegularExpressions.Regex.Match(num, $@"^{year}\s*-?\s*(.+)$");
        return m.Success ? m.Groups[1].Value.Trim() : num;
    }

    /// <summary>Prüft die Verbindung (PROPFIND auf das Stammverzeichnis).</summary>
    public async Task<(bool ok, string message)> TestConnectionAsync(string? url = null, string? user = null, string? pass = null)
    {
        var cfg = Config();
        url ??= cfg.url; user ??= cfg.user; pass ??= cfg.pass;
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            return (false, "Bitte URL, Benutzername und App-Passwort ausfüllen.");

        try
        {
            using var client = CreateClient(user, pass);
            client.Timeout = TimeSpan.FromSeconds(20);
            var req = new HttpRequestMessage(new HttpMethod("PROPFIND"), DavRoot(url, user));
            req.Headers.Add("Depth", "0");
            using var resp = await client.SendAsync(req);
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return (false, "Anmeldung fehlgeschlagen – Benutzername/App-Passwort prüfen.");
            if (resp.IsSuccessStatusCode || (int)resp.StatusCode == 207)
                return (true, "Verbindung erfolgreich.");
            return (false, $"Server antwortete mit {(int)resp.StatusCode} ({resp.StatusCode}).");
        }
        catch (Exception ex)
        {
            return (false, $"Fehler: {ex.Message}");
        }
    }

    /// <summary>Legt den Ordner (inkl. aller Elternordner) an, falls nicht vorhanden.</summary>
    public async Task EnsureFolderAsync(HttpClient client, string davRoot, string relativeFolder)
    {
        var segments = relativeFolder.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var cumulative = "";
        foreach (var seg in segments)
        {
            cumulative += "/" + Uri.EscapeDataString(seg);
            var req = new HttpRequestMessage(new HttpMethod("MKCOL"), davRoot + cumulative);
            using var resp = await client.SendAsync(req);
            // 201 Created = neu; 405 Method Not Allowed = existiert bereits -> beides ok.
            if (resp.StatusCode != HttpStatusCode.Created && (int)resp.StatusCode != 405)
            {
                if (resp.StatusCode == HttpStatusCode.Unauthorized)
                    throw new InvalidOperationException("Anmeldung fehlgeschlagen (App-Passwort?).");
                // 409 = Elternordner fehlt (sollte durch Reihenfolge nicht passieren); sonstige Fehler melden
                throw new InvalidOperationException($"Ordner '{seg}' konnte nicht angelegt werden ({(int)resp.StatusCode}).");
            }
        }
    }

    /// <summary>Lädt eine Datei hoch (legt den Zielordner vorher an).</summary>
    public async Task UploadAsync(string relativeFolder, string fileName, byte[] data, string contentType)
    {
        var cfg = Config();
        if (string.IsNullOrWhiteSpace(cfg.url) || string.IsNullOrWhiteSpace(cfg.user) || string.IsNullOrWhiteSpace(cfg.pass))
            throw new InvalidOperationException("Nextcloud ist nicht konfiguriert.");

        using var client = CreateClient(cfg.user, cfg.pass);
        var davRoot = DavRoot(cfg.url, cfg.user);
        await EnsureFolderAsync(client, davRoot, relativeFolder);

        var url = $"{davRoot}/{EncodePath(relativeFolder)}/{Uri.EscapeDataString(fileName)}";
        var content = new ByteArrayContent(data);
        content.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        using var resp = await client.PutAsync(url, content);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Upload von '{fileName}' fehlgeschlagen ({(int)resp.StatusCode}).");
    }

    /// <summary>
    /// Liefert die vorhandenen Dateinamen im Ordner (PROPFIND Depth 1), um doppelte
    /// Uploads zu vermeiden. Leeres Set, wenn Ordner fehlt/nicht erreichbar.
    /// </summary>
    public async Task<HashSet<string>> GetExistingFileNamesAsync(string relativeFolder)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cfg = Config();
        if (string.IsNullOrWhiteSpace(cfg.url) || string.IsNullOrWhiteSpace(cfg.user) || string.IsNullOrWhiteSpace(cfg.pass))
            return result;
        try
        {
            using var client = CreateClient(cfg.user, cfg.pass);
            client.Timeout = TimeSpan.FromSeconds(20);
            var davRoot = DavRoot(cfg.url, cfg.user);
            var req = new HttpRequestMessage(new HttpMethod("PROPFIND"), $"{davRoot}/{EncodePath(relativeFolder)}");
            req.Headers.Add("Depth", "1");
            using var resp = await client.SendAsync(req);
            if (!resp.IsSuccessStatusCode && (int)resp.StatusCode != 207) return result;

            var xml = await resp.Content.ReadAsStringAsync();
            XNamespace d = "DAV:";
            var doc = XDocument.Parse(xml);
            foreach (var href in doc.Descendants(d + "href"))
            {
                var raw = href.Value;
                // Ordner/Collections enden auf '/' -> überspringen; nur Dateien zählen.
                if (string.IsNullOrWhiteSpace(raw) || raw.EndsWith("/")) continue;
                var lastSeg = raw.Substring(raw.LastIndexOf('/') + 1);
                result.Add(Uri.UnescapeDataString(lastSeg));
            }
        }
        catch { }
        return result;
    }

    /// <summary>
    /// Anzahl Dateien in einem Ordner (PROPFIND Depth 1). -1 wenn Ordner fehlt/Fehler.
    /// </summary>
    public async Task<int> CountFilesAsync(string relativeFolder)
    {
        var cfg = Config();
        if (string.IsNullOrWhiteSpace(cfg.url) || string.IsNullOrWhiteSpace(cfg.user) || string.IsNullOrWhiteSpace(cfg.pass))
            return -1;
        try
        {
            using var client = CreateClient(cfg.user, cfg.pass);
            client.Timeout = TimeSpan.FromSeconds(20);
            var davRoot = DavRoot(cfg.url, cfg.user);
            var req = new HttpRequestMessage(new HttpMethod("PROPFIND"), $"{davRoot}/{EncodePath(relativeFolder)}");
            req.Headers.Add("Depth", "1");
            using var resp = await client.SendAsync(req);
            if ((int)resp.StatusCode == 404) return -1;
            if (!resp.IsSuccessStatusCode && (int)resp.StatusCode != 207) return -1;

            var xml = await resp.Content.ReadAsStringAsync();
            XNamespace d = "DAV:";
            var doc = XDocument.Parse(xml);
            // Jede <response> ist eine Ressource; die erste ist der Ordner selbst.
            var responses = doc.Descendants(d + "response").Count();
            return Math.Max(0, responses - 1);
        }
        catch
        {
            return -1;
        }
    }
}
