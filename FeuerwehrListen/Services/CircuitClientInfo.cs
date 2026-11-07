using Microsoft.AspNetCore.DataProtection;

namespace FeuerwehrListen.Services;

/// <summary>
/// Client-IP des Nutzers fuer den Blazor-SERVER-Modus (Alt-Geraete). Im laufenden Circuit
/// gibt es keinen HttpContext - die Self-Calls kaemen sonst "vom Server selbst" und die
/// Login-Whitelist koennte nicht greifen (Kiosk im Geraetehaus waere gesperrt; fail-closed).
///
/// Die IP wird beim ersten HTTP-Request (App.razor) mit Data Protection versiegelt als
/// Komponenten-Parameter mitgegeben und im Circuit wieder entsiegelt. Der Client kann den
/// Wert nicht faelschen (signiert + verschluesselt), nur unveraendert zurueckgeben.
/// </summary>
public sealed class CircuitClientInfo
{
    public const string Purpose = "FeuerwehrListen.CircuitClientIp.v1";

    public string? ClientIp { get; private set; }
    /// <summary>Geraete-Cookie (Geraetekonto) des ersten Requests - wird im Circuit als Header weitergereicht.</summary>
    public string? DeviceToken { get; private set; }

    /// <summary>Versiegelt "ip|geraetetoken" (Token darf leer sein).</summary>
    public static string Seal(IDataProtectionProvider provider, string ip, string? deviceToken) =>
        provider.CreateProtector(Purpose).Protect(ip + "|" + (deviceToken ?? ""));

    public void Unseal(IDataProtectionProvider provider, string? token)
    {
        if (string.IsNullOrEmpty(token)) return;
        try
        {
            var raw = provider.CreateProtector(Purpose).Unprotect(token);
            var sep = raw.IndexOf('|');
            ClientIp = sep < 0 ? raw : raw[..sep];
            DeviceToken = sep < 0 || sep == raw.Length - 1 ? null : raw[(sep + 1)..];
        }
        catch { ClientIp = null; DeviceToken = null; /* manipuliert -> fail-closed */ }
    }
}
