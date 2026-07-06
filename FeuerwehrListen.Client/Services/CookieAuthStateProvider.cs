using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace FeuerwehrListen.Client.Services;

/// <summary>
/// Auth-Status im WASM-Client aus der Cookie-Session (Endpoint /client-api/auth/me).
/// </summary>
public class CookieAuthStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _http;

    // Memoisiertes Ergebnis: CascadingAuthenticationState/NavMenu/AuthGuard teilen sich EINEN Fetch.
    private Task<AuthenticationState>? _cached;
    // Zuletzt bekannter Zustand, um bei transienten Netzfehlern nicht faelschlich abzumelden.
    private AuthenticationState? _lastKnown;

    public CookieAuthStateProvider(HttpClient http) => _http = http;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => _cached ??= FetchAsync();

    private async Task<AuthenticationState> FetchAsync()
    {
        try
        {
            var me = await _http.GetFromJsonAsync<MeDto>("client-api/auth/me");
            AuthenticationState state;
            if (me?.Authenticated == true)
            {
                var claims = new List<Claim> { new(ClaimTypes.Name, me.Username ?? "") };
                if (me.IsAdmin) claims.Add(new(ClaimTypes.Role, "Admin"));
                claims.Add(new("FirstName", me.FirstName ?? ""));
                claims.Add(new("LastName", me.LastName ?? ""));
                var identity = new ClaimsIdentity(claims, "FwCookie", ClaimTypes.Name, ClaimTypes.Role);
                state = new AuthenticationState(new ClaimsPrincipal(identity));
            }
            else
            {
                // Echte "authenticated=false"-Antwort -> anonym.
                state = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }
            _lastKnown = state;
            return state;
        }
        catch
        {
            // Netzfehler/Timeout != abgemeldet: zuletzt bekannten Zustand behalten, sonst anonym.
            // Fehlgeschlagenen Fetch NICHT dauerhaft cachen -> naechster Aufruf versucht erneut.
            _cached = null;
            return _lastKnown ?? new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    public void NotifyChanged()
    {
        _cached = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private record MeDto(bool Authenticated, string? Username, bool IsAdmin, string? FirstName, string? LastName);
}
