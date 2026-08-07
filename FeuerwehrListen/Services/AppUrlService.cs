using FeuerwehrListen.Models;

namespace FeuerwehrListen.Services;

/// <summary>
/// Liefert die oeffentliche Basis-URL fuer absolute Links (z. B. Freigabe-Links in Mails).
/// Reihenfolge: Einstellung App.BaseUrl (im Admin pflegbar) &gt; AppSettings:BaseUrl aus der
/// Konfiguration &gt; beim ersten Request erfasster Host-Header.
///
/// Die Einstellung hat bewusst Vorrang: hinter einem Reverse-Proxy ist der Host-Header
/// die interne Adresse, ein Link darauf waere fuer den Empfaenger wertlos.
/// </summary>
public sealed class AppUrlService
{
    private readonly SettingsService _settings;
    private readonly IConfiguration _config;
    private readonly AppBaseUrlProvider _provider;

    public AppUrlService(SettingsService settings, IConfiguration config, AppBaseUrlProvider provider)
    {
        _settings = settings;
        _config = config;
        _provider = provider;
    }

    /// <summary>Basis-URL ohne abschliessenden Schraegstrich, oder leer wenn nichts bekannt ist.</summary>
    public string GetBaseUrl()
    {
        var fromSettings = _settings.GetSetting(SettingKeys.AppBaseUrl);
        if (!string.IsNullOrWhiteSpace(fromSettings))
            return Normalize(fromSettings);

        var fromConfig = _config["AppSettings:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(fromConfig))
            return Normalize(fromConfig);

        return Normalize(_provider.BaseUrl ?? string.Empty);
    }

    /// <summary>Absolute URL zu einem relativen Pfad. Ist keine Basis bekannt, bleibt der Pfad relativ.</summary>
    public string BuildUrl(string relativePath)
    {
        var path = (relativePath ?? string.Empty).TrimStart('/');
        var baseUrl = GetBaseUrl();
        return string.IsNullOrEmpty(baseUrl) ? "/" + path : $"{baseUrl}/{path}";
    }

    /// <summary>
    /// Absolute URL fuer Freigabe-Links. Nutzt App.ApproveBaseUrl, wenn gesetzt -
    /// etwa eine eigene passwortfreie Subdomain -, sonst die normale Basis-URL.
    /// </summary>
    public string BuildApproveUrl(string relativePath)
    {
        var path = (relativePath ?? string.Empty).TrimStart('/');
        var special = _settings.GetSetting(SettingKeys.AppApproveBaseUrl);
        if (!string.IsNullOrWhiteSpace(special))
            return $"{Normalize(special)}/{path}";
        return BuildUrl(path);
    }

    /// <summary>True, wenn absolute Links erzeugt werden koennen (sonst kaputte Mail-Links).</summary>
    public bool HasAbsoluteBase() => !string.IsNullOrEmpty(GetBaseUrl())
        || !string.IsNullOrWhiteSpace(_settings.GetSetting(SettingKeys.AppApproveBaseUrl));

    private static string Normalize(string url) => (url ?? string.Empty).Trim().TrimEnd('/');
}
