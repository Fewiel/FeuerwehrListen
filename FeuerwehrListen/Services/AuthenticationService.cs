using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FeuerwehrListen.Models;
using FeuerwehrListen.Data;
using LinqToDB;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace FeuerwehrListen.Services;

public enum QrLoginResult
{
    Success,
    InvalidCode,
    PinRequired,
    InvalidPin
}

public class AuthenticationService : AuthenticationStateProvider, IHostEnvironmentAuthenticationStateProvider
{
    private User? _currentUser;
    private readonly IServiceProvider _serviceProvider;
    private readonly ProtectedLocalStorage _localStorage;

    // Vom Framework beim (Pre-)Rendern mit der HttpContext-Identitaet (Cookie) gesetzt.
    private Task<AuthenticationState>? _hostAuthStateTask;

    public AuthenticationService(IServiceProvider serviceProvider, ProtectedLocalStorage localStorage)
    {
        _serviceProvider = serviceProvider;
        _localStorage = localStorage;
    }

    public User? CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser != null;
    public bool IsAdmin => _currentUser?.Role == UserRole.Admin;

    // IHostEnvironmentAuthenticationStateProvider: Cookie-Login (WASM /client-api/auth/*)
    // wird so auch fuer die (noch) serverseitig gerenderten Admin-Seiten sichtbar.
    public void SetAuthenticationState(Task<AuthenticationState> authenticationStateTask)
    {
        _hostAuthStateTask = authenticationStateTask ?? throw new ArgumentNullException(nameof(authenticationStateTask));
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_currentUser == null && _hostAuthStateTask != null)
        {
            try
            {
                var hostState = await _hostAuthStateTask;
                if (hostState.User.Identity?.IsAuthenticated == true)
                    SeedFromPrincipal(hostState.User);
            }
            catch { }
        }
        return BuildAuthState();
    }

    private void SeedFromPrincipal(ClaimsPrincipal principal)
    {
        var roleStr = principal.FindFirst(ClaimTypes.Role)?.Value;
        _currentUser = new User
        {
            Username = principal.Identity?.Name ?? string.Empty,
            Role = Enum.TryParse<UserRole>(roleStr, out var role) ? role : UserRole.User,
            FirstName = principal.FindFirst("FirstName")?.Value ?? string.Empty,
            LastName = principal.FindFirst("LastName")?.Value ?? string.Empty
        };
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
            new Claim(ClaimTypes.Role, _currentUser.Role.ToString()),
            // Vor-/Nachname mitgeben, damit das NavMenu im Server-Modus die richtigen
            // Initialen zeigt (konsistent zu /client-api/auth/me und SeedFromPrincipal).
            new Claim("FirstName", _currentUser.FirstName ?? string.Empty),
            new Claim("LastName", _currentUser.LastName ?? string.Empty)
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

        // Zuerst die Cookie-Session (WASM-Login) uebernehmen, falls vorhanden.
        await GetAuthenticationStateAsync();
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

        if (!VerifyPassword(user.PasswordHash, password))
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

    public async Task<User?> FindByQrCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbConnection>();
        return await db.Users.FirstOrDefaultAsync(x => x.QrAuthCode == code);
    }

    public async Task<QrLoginResult> LoginWithQrAsync(string code, string? pin = null)
    {
        var user = await FindByQrCodeAsync(code);
        if (user == null) return QrLoginResult.InvalidCode;

        // Admin-User need a PIN as 2FA
        if (user.Role == UserRole.Admin && !string.IsNullOrWhiteSpace(user.AdminPin))
        {
            if (string.IsNullOrWhiteSpace(pin))
                return QrLoginResult.PinRequired;
            if (user.AdminPin != pin.Trim())
                return QrLoginResult.InvalidPin;
        }

        _currentUser = user;

        try
        {
            await _localStorage.SetAsync("auth_user", user.Username);
            await _localStorage.SetAsync("auth_hash", user.PasswordHash);
        }
        catch { }

        return QrLoginResult.Success;
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

        if (!VerifyPassword(_currentUser.PasswordHash, oldPassword))
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

    // TODO F27: Passwoerter werden mit ungesalzenem SHA256 gehasht. Wuenschenswert waere
    // PBKDF2 (Rfc2898DeriveBytes, ~100k Iter., 16-Byte-Salt). Ein transparentes Re-Hashing
    // beim Login wurde hier BEWUSST NICHT umgesetzt, weil der Restore-Pfad
    // (TryRestoreSessionAsync) den in ProtectedLocalStorage gespeicherten Hash 1:1 gegen
    // User.PasswordHash vergleicht (Hash-gegen-Hash, nicht Passwort-gegen-Hash). Ein Wechsel
    // des DB-Hashes auf PBKDF2 (zufaelliges Salt -> anderer Wert) wuerde diesen Vergleich
    // brechen und angemeldete Nutzer beim naechsten Seitenaufruf ausloggen. Eine Migration
    // muesste zuerst den localStorage-Restore von "gespeicherter Hash == DB-Hash" auf einen
    // stabilen Session-Token entkoppeln; erst danach ist PBKDF2 gefahrlos einfuehrbar.
    // Bis dahin bleibt HashPassword bei SHA256; VerifyPassword ist bereits vorwaerts-
    // kompatibel und erkennt kuenftige "pbkdf2$"-Hashes.
    public static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Prueft ein Klartext-Passwort gegen einen gespeicherten Hash. Erkennt sowohl das
    /// (aktuelle) Legacy-SHA256-Format als auch ein zukuenftiges PBKDF2-Format
    /// ("pbkdf2$&lt;iter&gt;$&lt;salt-b64&gt;$&lt;hash-b64&gt;"). Vorwaerts-kompatibel, damit
    /// alle Vergleichsstellen bereits jetzt einheitlich VerifyPassword nutzen koennen.
    /// </summary>
    public static bool VerifyPassword(string? storedHash, string? inputPassword)
    {
        if (string.IsNullOrEmpty(storedHash)) return false;
        inputPassword ??= string.Empty;

        if (storedHash.StartsWith("pbkdf2$", StringComparison.Ordinal))
        {
            var parts = storedHash.Split('$');
            if (parts.Length != 4) return false;
            if (!int.TryParse(parts[1], out var iterations) || iterations <= 0) return false;
            byte[] salt, expected;
            try { salt = Convert.FromBase64String(parts[2]); expected = Convert.FromBase64String(parts[3]); }
            catch (FormatException) { return false; }
            using var pbkdf2 = new Rfc2898DeriveBytes(inputPassword, salt, iterations, HashAlgorithmName.SHA256);
            var actual = pbkdf2.GetBytes(expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        // Legacy: ungesalzenes SHA256 (Base64). Zeitkonstanter Vergleich.
        var a = Encoding.UTF8.GetBytes(HashPassword(inputPassword));
        var b = Encoding.UTF8.GetBytes(storedHash);
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
