using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

/// <summary>
/// Benannter Berechtigungsschluessel (z.B. "alarmmonitor_admin"), den Admins in der UI anlegen
/// und Nutzern zuweisen. Externe SSO-Clients fordern solche Keys an; nur Nutzer mit passendem
/// Key erhalten Zugang. Die Zuordnung Nutzer&lt;-&gt;Key liegt in <see cref="UserPermissionKey"/>.
/// </summary>
[Table("PermissionKey")]
public class PermissionKey
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }

    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [Column("Description")]
    public string Description { get; set; } = string.Empty;

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }
}
