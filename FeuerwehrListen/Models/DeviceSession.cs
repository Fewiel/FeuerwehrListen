using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

/// <summary>
/// Dauerhafte Anmeldung eines Geraets mit einem Geraetekonto (Rolle Device). Das Geraet traegt
/// ein langlebiges Cookie mit einem zufaelligen Token; hier liegt nur dessen Hash. Sperren =
/// RevokedAt setzen - greift beim naechsten Aufruf.
/// </summary>
[Table("DeviceSession")]
public class DeviceSession
{
    [PrimaryKey, Identity, Column("Id")] public int Id { get; set; }
    [Column("UserId")] public int UserId { get; set; }
    [Column("TokenHash")] public string TokenHash { get; set; } = string.Empty;
    [Column("UserAgent")] public string UserAgent { get; set; } = string.Empty;
    [Column("CreatedAt")] public DateTime CreatedAt { get; set; }
    [Column("LastSeenAt")] public DateTime LastSeenAt { get; set; }
    [Column("LastIp")] public string? LastIp { get; set; }
    [Column("RevokedAt")] public DateTime? RevokedAt { get; set; }
}
