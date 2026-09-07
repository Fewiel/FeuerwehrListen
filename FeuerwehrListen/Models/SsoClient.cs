using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

/// <summary>
/// Registriertes externes System (OAuth2-Client, z.B. "alarmmonitor"), das FeuerwehrListen als
/// Identity-Provider nutzt. Das Secret wird nur gehasht gespeichert (SHA-256 via
/// AuthenticationService) und bei der Anlage einmalig im Klartext angezeigt.
/// </summary>
[Table("SsoClient")]
public class SsoClient
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }

    [Column("ClientId")]
    public string ClientId { get; set; } = string.Empty;

    [Column("ClientSecretHash")]
    public string ClientSecretHash { get; set; } = string.Empty;

    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Erlaubte redirect_uris, eine pro Zeile.</summary>
    [Column("RedirectUris")]
    public string RedirectUris { get; set; } = string.Empty;

    /// <summary>Benoetigte Keys (kommagetrennt). Leer = jeder angemeldete Nutzer ist erlaubt.</summary>
    [Column("RequiredKeys")]
    public string RequiredKeys { get; set; } = string.Empty;

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }
}
