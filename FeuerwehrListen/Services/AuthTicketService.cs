using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace FeuerwehrListen.Services;

/// <summary>
/// Einmal-Tickets fuer den Login-Abschluss. Hintergrund: Im Server-Modus (SignalR) laeuft
/// der POST /client-api/auth/login server-INTERN (SelfHttpClient) - die Set-Cookie-Antwort
/// erreicht den Browser NIE, der Nutzer bleibt anonym. Loesung: Der Cookie-SignIn muss in
/// einer ECHTEN Top-Level-Browser-Navigation (GET /client-api/auth/complete) passieren.
/// Der Login/QR-Endpoint gibt daher nur ein kurzlebiges Ticket zurueck; die Client-Seite
/// navigiert den Browser hart auf /client-api/auth/complete?ticket=..., wo der Cookie in
/// einer echten Browser-GET-Response gesetzt wird (funktioniert in WASM UND Server-Modus).
/// Ticket: kryptografisch zufaellig, an die userId gebunden, 60s gueltig, EINMALVERWENDUNG.
/// </summary>
public sealed class AuthTicketService
{
    private sealed record Entry(int UserId, DateTime ExpiresUtc, bool IsMember = false);

    private readonly ConcurrentDictionary<string, Entry> _tickets = new();
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    /// <summary>Erzeugt ein Einmal-Ticket, das die userId sicher bindet (60s gueltig).</summary>
    public string CreateTicket(int userId) => Create(new Entry(userId, DateTime.UtcNow.Add(Lifetime)));

    /// <summary>Ticket fuer ein Mitglieder-Konto (Mitgliederbereich); id = Member.Id.</summary>
    public string CreateMemberTicket(int memberId) => Create(new Entry(memberId, DateTime.UtcNow.Add(Lifetime), IsMember: true));

    private string Create(Entry entry)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _tickets[token] = entry;
        return token;
    }

    /// <summary>Loest ein Ticket EINMALIG ein: entfernt es und liefert die userId zurueck.</summary>
    public bool TryConsume(string? ticket, out int userId, out bool isMember)
    {
        userId = 0; isMember = false;
        if (string.IsNullOrEmpty(ticket)) return false;
        if (!_tickets.TryRemove(ticket, out var entry)) return false;
        if (entry.ExpiresUtc < DateTime.UtcNow) return false;
        userId = entry.UserId;
        isMember = entry.IsMember;
        return true;
    }
}
