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

    public CookieAuthStateProvider(HttpClient http) => _http = http;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var me = await _http.GetFromJsonAsync<MeDto>("client-api/auth/me");
            if (me?.Authenticated == true)
            {
                var claims = new List<Claim> { new(ClaimTypes.Name, me.Username ?? "") };
                if (me.IsAdmin) claims.Add(new(ClaimTypes.Role, "Admin"));
                claims.Add(new("FirstName", me.FirstName ?? ""));
                claims.Add(new("LastName", me.LastName ?? ""));
                var identity = new ClaimsIdentity(claims, "FwCookie", ClaimTypes.Name, ClaimTypes.Role);
                return new AuthenticationState(new ClaimsPrincipal(identity));
            }
        }
        catch { }
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    public void NotifyChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private record MeDto(bool Authenticated, string? Username, bool IsAdmin, string? FirstName, string? LastName);
}
