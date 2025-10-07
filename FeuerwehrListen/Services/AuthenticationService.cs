using System.Security.Cryptography;
using System.Text;
using FeuerwehrListen.Models;
using FeuerwehrListen.Data;
using LinqToDB;

namespace FeuerwehrListen.Services;

public class AuthenticationService
{
    private User? _currentUser;
    private readonly IServiceProvider _serviceProvider;

    public AuthenticationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public event Action? OnAuthStateChanged;

    public User? CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser != null;
    public bool IsAdmin => _currentUser?.Role == UserRole.Admin;

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
        NotifyAuthStateChanged();
        return true;
    }

    public void Logout()
    {
        _currentUser = null;
        NotifyAuthStateChanged();
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

    private void NotifyAuthStateChanged() => OnAuthStateChanged?.Invoke();
}

