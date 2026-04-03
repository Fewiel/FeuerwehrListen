using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FeuerwehrListen.Models;
using FeuerwehrListen.Data;
using LinqToDB;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace FeuerwehrListen.Services;

public class AuthenticationService : AuthenticationStateProvider
{
    private User? _currentUser;
    private readonly IServiceProvider _serviceProvider;
    private readonly ProtectedLocalStorage _localStorage;

    public AuthenticationService(IServiceProvider serviceProvider, ProtectedLocalStorage localStorage)
    {
        _serviceProvider = serviceProvider;
        _localStorage = localStorage;
    }

    public User? CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser != null;
    public bool IsAdmin => _currentUser?.Role == UserRole.Admin;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(BuildAuthState());
    }

    private AuthenticationState BuildAuthState()
    {
        if (_currentUser == null)
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, _currentUser.Username),
            new Claim(ClaimTypes.Role, _currentUser.Role.ToString())
        };
        var identity = new ClaimsIdentity(claims, "apiauth");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    /// <summary>
    /// Tries to restore the session from ProtectedLocalStorage.
    /// Must be called from OnAfterRenderAsync.
    /// </summary>
    public async Task<bool> TryRestoreSessionAsync()
    {
        if (_currentUser != null)
            return true;

        try
        {
            var usernameResult = await _localStorage.GetAsync<string>("auth_user");
            var hashResult = await _localStorage.GetAsync<string>("auth_hash");

            if (!usernameResult.Success || !hashResult.Success)
                return false;

            var username = usernameResult.Value;
            var hash = hashResult.Value;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(hash))
                return false;

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbConnection>();
            var user = await db.Users.FirstOrDefaultAsync(x => x.Username == username);

            if (user != null && user.PasswordHash == hash)
            {
                _currentUser = user;
                // Don't call NotifyAuthenticationStateChanged here.
                // The caller (AdminAuthCheck) will call StateHasChanged() which
                // causes AuthorizeView to re-evaluate GetAuthenticationStateAsync().
                return true;
            }
            else
            {
                await _localStorage.DeleteAsync("auth_user");
                await _localStorage.DeleteAsync("auth_hash");
                return false;
            }
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbConnection>();

        var user = await db.Users.FirstOrDefaultAsync(x => x.Username == username);
        if (user == null)
            return false;

        var passwordHash = HashPassword(password);
        if (user.PasswordHash != passwordHash)
            return false;

        _currentUser = user;

        try
        {
            await _localStorage.SetAsync("auth_user", user.Username);
            await _localStorage.SetAsync("auth_hash", user.PasswordHash);
        }
        catch
        {
            // LocalStorage not available (e.g. prerendering)
        }

        // Don't call NotifyAuthenticationStateChanged — Login.razor navigates
        // after this, so the new page will get fresh auth state.
        return true;
    }

    public async Task LogoutAsync()
    {
        _currentUser = null;

        try
        {
            await _localStorage.DeleteAsync("auth_user");
            await _localStorage.DeleteAsync("auth_hash");
        }
        catch
        {
            // LocalStorage not available
        }
    }

    public async Task<bool> ChangePasswordAsync(string oldPassword, string newPassword)
    {
        if (_currentUser == null)
            return false;

        var oldPasswordHash = HashPassword(oldPassword);
        if (_currentUser.PasswordHash != oldPasswordHash)
            return false;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbConnection>();

        _currentUser.PasswordHash = HashPassword(newPassword);
        await db.UpdateAsync(_currentUser);

        try
        {
            await _localStorage.SetAsync("auth_hash", _currentUser.PasswordHash);
        }
        catch
        {
            // LocalStorage not available
        }

        return true;
    }

    public static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
