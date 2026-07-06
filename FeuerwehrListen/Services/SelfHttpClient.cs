using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Components.Authorization;

namespace FeuerwehrListen.Services;

/// <summary>App-weite Basis-URL (scheme://host), beim ersten Request einmalig erfasst.
/// Waehrend eines Blazor-Server-Circuits ist kein HttpContext verfuegbar - daher brauchen
/// die server-seitigen Self-Calls eine Quelle fuer die BaseAddress ohne HttpContext.</summary>
public sealed class AppBaseUrlProvider
{
    public string? BaseUrl { get; set; }
}

/// <summary>Prozess-geheimer Schluessel (nur im Server-Speicher, verlaesst die App nie).
/// Legitimiert interne Self-Calls, die im Server-Modus die Nutzer-Identitaet per Header
/// weiterreichen (weil im Circuit kein Auth-Cookie verfuegbar ist).</summary>
public sealed class InternalAuthSecret
{
    public string Value { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}

/// <summary>
/// HttpClient-Handler fuer server-gerenderte Client-Komponenten (Server-Modus / Alt-Geraete,
/// [[ios12-legacy-ui]]). Die Komponenten holen ihre Daten per HttpClient von den eigenen
/// /client-api-Endpoints:
/// - Beim Prerender ist ein HttpContext da -> echtes Auth-Cookie weiterreichen.
/// - Im laufenden Circuit gibt es keinen HttpContext -> Identitaet des angemeldeten Nutzers
///   (aus dem AuthenticationStateProvider) per internem, prozess-geheimem Header uebergeben;
///   die InternalIdentityMiddleware macht daraus serverseitig wieder eine Identity.
/// </summary>
public sealed class SelfCookieHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _accessor;
    private readonly AuthenticationStateProvider _auth;
    private readonly InternalAuthSecret _secret;
    private readonly AppBaseUrlProvider _baseUrl;

    public SelfCookieHandler(IHttpContextAccessor accessor, AuthenticationStateProvider auth, InternalAuthSecret secret, AppBaseUrlProvider baseUrl)
    {
        _accessor = accessor;
        _auth = auth;
        _secret = secret;
        _baseUrl = baseUrl;
    }

    /// <summary>Prueft, ob das Request-Ziel der eigene Host (Host+Port) ist. Nur dann duerfen
    /// Auth-Cookie bzw. das prozess-geheime X-Fw-Internal angehaengt werden - sonst wuerde ein
    /// Aufruf an eine frei konfigurierbare Fremd-URL (z. B. der 3D-Tag-Helfer) das Geheimnis
    /// oder das Cookie leaken (SSRF/Secret-Leak).</summary>
    private bool IsSelfHost(Uri? target)
    {
        if (target == null) return false;
        // Eigenen Host bestimmen: bevorzugt aus dem aktuellen HttpContext (Prerender),
        // sonst aus der beim ersten Request erfassten BaseUrl (gilt auch im Circuit).
        var ctx = _accessor.HttpContext;
        string? ownHost = null; int ownPort = -1; string? ownScheme = null;
        if (ctx != null)
        {
            ownHost = ctx.Request.Host.Host;
            ownPort = ctx.Request.Host.Port ?? -1;
            ownScheme = ctx.Request.Scheme;
        }
        else if (!string.IsNullOrEmpty(_baseUrl.BaseUrl) && Uri.TryCreate(_baseUrl.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            ownHost = baseUri.Host;
            ownPort = baseUri.IsDefaultPort ? -1 : baseUri.Port;
            ownScheme = baseUri.Scheme;
        }
        if (string.IsNullOrEmpty(ownHost)) return false;

        if (!string.Equals(target.Host, ownHost, StringComparison.OrdinalIgnoreCase)) return false;
        // Port vergleichen (Standard-Ports beruecksichtigen).
        var targetPort = target.IsDefaultPort ? -1 : target.Port;
        var normOwnPort = ownPort;
        // Wenn eigener Port dem Standard-Port des Ziel-Schemas entspricht, als "default" behandeln.
        if (normOwnPort != -1 && ownScheme != null &&
            ((ownScheme == "https" && normOwnPort == 443) || (ownScheme == "http" && normOwnPort == 80)))
            normOwnPort = -1;
        return targetPort == normOwnPort;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Nur fuer den eigenen Host Auth weiterreichen - bei Fremdhosts NICHTS anhaengen.
        if (!IsSelfHost(request.RequestUri))
            return await base.SendAsync(request, cancellationToken);

        var ctx = _accessor.HttpContext;
        if (ctx != null)
        {
            if (!request.Headers.Contains("Cookie") && ctx.Request.Headers.TryGetValue("Cookie", out var cookie))
                request.Headers.TryAddWithoutValidation("Cookie", cookie.ToString());
        }
        else
        {
            // Circuit: angemeldeten Nutzer per internem Header weiterreichen.
            try
            {
                var user = (await _auth.GetAuthenticationStateAsync()).User;
                var roleList = string.Join(",", user.FindAll(ClaimTypes.Role).Select(c => c.Value)
                    .Concat(user.FindAll("role").Select(c => c.Value)));
                if (user.Identity?.IsAuthenticated == true)
                {
                    request.Headers.TryAddWithoutValidation("X-Fw-Internal", _secret.Value);
                    request.Headers.TryAddWithoutValidation("X-Fw-User", user.Identity.Name ?? "");
                    request.Headers.TryAddWithoutValidation("X-Fw-Role", roleList);
                }
            }
            catch { /* nicht angemeldet / kein Auth-Kontext */ }
        }
        return await base.SendAsync(request, cancellationToken);
    }
}
