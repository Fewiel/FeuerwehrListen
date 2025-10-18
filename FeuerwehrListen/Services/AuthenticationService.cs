using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FeuerwehrListen.Models;
using FeuerwehrListen.Data;
using LinqToDB;
using Microsoft.AspNetCore.Components.Authorization;

namespace FeuerwehrListen.Services;

public class AuthenticationService : AuthenticationStateProvider
{
    private User? _currentUser;
    private readonly IServiceProvider _serviceProvider;

    public AuthenticationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public User? CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser != null;
    public bool IsAdmin => _currentUser?.Role == UserRole.Admin;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
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
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        return true;
    }

    public void Logout()
    {
        _currentUser = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
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

