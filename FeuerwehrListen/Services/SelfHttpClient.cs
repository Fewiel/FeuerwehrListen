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

    public SelfCookieHandler(IHttpContextAccessor accessor, AuthenticationStateProvider auth, InternalAuthSecret secret)
    {
        _accessor = accessor;
        _auth = auth;
        _secret = secret;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
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
