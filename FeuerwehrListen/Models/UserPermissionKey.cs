using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

/// <summary>Zuordnungstabelle (M:N) zwischen <see cref="User"/> und <see cref="PermissionKey"/>.</summary>
[Table("UserPermissionKey")]
public class UserPermissionKey
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }

    [Column("UserId")]
    public int UserId { get; set; }

    [Column("PermissionKeyId")]
    public int PermissionKeyId { get; set; }
}
