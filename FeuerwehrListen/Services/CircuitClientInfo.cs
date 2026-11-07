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

    public static string Seal(IDataProtectionProvider provider, string ip) =>
        provider.CreateProtector(Purpose).Protect(ip);

    public void Unseal(IDataProtectionProvider provider, string? token)
    {
        if (string.IsNullOrEmpty(token)) return;
        try { ClientIp = provider.CreateProtector(Purpose).Unprotect(token); }
        catch { ClientIp = null; /* manipuliert oder fremder Schluessel -> keine IP -> fail-closed */ }
    }
}
