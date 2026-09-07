using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

[Table("User")]
public class User
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }
    
    [Column("Username")]
    public string Username { get; set; } = string.Empty;
    
    [Column("PasswordHash")]
    public string PasswordHash { get; set; } = string.Empty;
    
    [Column("FirstName")]
    public string FirstName { get; set; } = string.Empty;
    
    [Column("LastName")]
    public string LastName { get; set; } = string.Empty;
    
    [Column("Email")]
    public string Email { get; set; } = string.Empty;
    
    [Column("Role")]
    public UserRole Role { get; set; }

    [Column("QrAuthCode")]
    public string? QrAuthCode { get; set; }

    [Column("AdminPin")]
    public string? AdminPin { get; set; }

    // Steuert den Zugriff auf das Listen-Tool. Bestandsnutzer werden per Migration auf true
    // gesetzt; reine SSO-Konten koennen ohne Listen-Zugriff (false) angelegt werden.
    [Column("HasListAccess")]
    public bool HasListAccess { get; set; } = true;

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }
}

