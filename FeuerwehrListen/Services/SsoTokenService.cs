using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace FeuerwehrListen.Services;

/// <summary>
/// In-Memory-Speicher fuer den OAuth2-Authorization-Code-Flow des internen SSO-Providers.
/// Bewusst fluechtig (wie <see cref="AuthTicketService"/>): Auth-Codes leben nur Sekunden,
/// Access-Tokens wenige Minuten - ein Neustart erzwingt lediglich einen erneuten Login.
///
/// Auth-Code: einmalig, 60s gueltig, gebunden an Nutzer + Client + redirect_uri + gewaehrte Keys.
/// Access-Token: opak (Bearer), 10min gueltig, wird gegen die Nutzer-Identitaet an /sso/userinfo
/// eingetauscht.
/// </summary>
public sealed class SsoTokenService
{
    public sealed record CodeData(int UserId, string ClientId, string RedirectUri, string[] GrantedKeys);
    public sealed record TokenData(int UserId, string ClientId, string[] GrantedKeys);

    private sealed record CodeEntry(CodeData Data, DateTime ExpiresUtc);
    private sealed record TokenEntry(TokenData Data, DateTime ExpiresUtc);

    private readonly ConcurrentDictionary<string, CodeEntry> _codes = new();
    private readonly ConcurrentDictionary<string, TokenEntry> _tokens = new();

    private static readonly TimeSpan CodeLifetime = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(10);

    /// <summary>Erzeugt einen einmaligen Auth-Code (60s gueltig).</summary>
    public string CreateAuthCode(int userId, string clientId, string redirectUri, string[] grantedKeys)
    {
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _codes[code] = new CodeEntry(new CodeData(userId, clientId, redirectUri, grantedKeys),
            DateTime.UtcNow.Add(CodeLifetime));
        return code;
    }

    /// <summary>Loest einen Auth-Code EINMALIG ein.</summary>
    public bool TryConsumeAuthCode(string? code, out CodeData data)
    {
        data = default!;
        if (string.IsNullOrEmpty(code)) return false;
        if (!_codes.TryRemove(code, out var entry)) return false;
        if (entry.ExpiresUtc < DateTime.UtcNow) return false;
        data = entry.Data;
        return true;
    }

    /// <summary>Erzeugt ein opakes Access-Token (10min gueltig).</summary>
    public string CreateAccessToken(int userId, string clientId, string[] grantedKeys)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _tokens[token] = new TokenEntry(new TokenData(userId, clientId, grantedKeys),
            DateTime.UtcNow.Add(TokenLifetime));
        return token;
    }

    /// <summary>Liest die Daten zu einem Access-Token (nicht verbrauchend, mit Ablaufpruefung).</summary>
    public bool TryGetAccessToken(string? token, out TokenData data)
    {
        data = default!;
        if (string.IsNullOrEmpty(token)) return false;
        if (!_tokens.TryGetValue(token, out var entry)) return false;
        if (entry.ExpiresUtc < DateTime.UtcNow)
        {
            _tokens.TryRemove(token, out _);
            return false;
        }
        data = entry.Data;
        return true;
    }
}
