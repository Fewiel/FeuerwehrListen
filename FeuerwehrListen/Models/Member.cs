using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

[Table("Member")]
public class Member
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }
    
    [Column("MemberNumber")]
    public string MemberNumber { get; set; } = string.Empty;
    
    [Column("FirstName")]
    public string FirstName { get; set; } = string.Empty;
    
    [Column("LastName")]
    public string LastName { get; set; } = string.Empty;
    
    [Column("IsActive")]
    public bool IsActive { get; set; }

    [Column("UnitNumber")]
    public int? UnitNumber { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    // --- Mitgliederbereich (Migration 33) ---

    /// <summary>Login fuer den Mitgliederbereich; null = noch kein Zugang. Wird nur im
    /// Passwort-Vergabe-Modus am Gerätehaus-Tablet (Admin anwesend) bzw. vom Mitglied selbst gesetzt.</summary>
    [Column("PasswordHash")]
    public string? PasswordHash { get; set; }

    [Column("PasswordSetAt")]
    public DateTime? PasswordSetAt { get; set; }

    /// <summary>Admin, der den Vergabe-Modus betreut hat.</summary>
    [Column("PasswordSetByUserId")]
    public int? PasswordSetByUserId { get; set; }

    /// <summary>Freigegebene Bereiche, kommagetrennt (wie User.ModuleAccess, aber nie "*").</summary>
    [Column("ModuleAccess")]
    public string ModuleAccess { get; set; } = string.Empty;

    /// <summary>0 = kein, 1 = grosser, 2 = kleiner Fuehrungskreis (klein steht ueber gross).</summary>
    [Column("LeadershipCircle")]
    public int LeadershipCircle { get; set; }

    [Column("Rank")]
    public string? Rank { get; set; }

    /// <summary>Geburtsdatum (Dienstausweis, Identitaetspruefung bei der Passwort-Vergabe).</summary>
    [Column("BirthDate")]
    public DateTime? BirthDate { get; set; }

    [Column("Email")]
    public string? Email { get; set; }

    /// <summary>Dateiname des Lichtbilds im Datenverzeichnis (photos/).</summary>
    [Column("PhotoFile")]
    public string? PhotoFile { get; set; }

    [Column("PhotoUpdatedAt")]
    public DateTime? PhotoUpdatedAt { get; set; }

    /// <summary>Zufaellige Kennung des Dienstausweises fuer den Pruef-Link /ausweis/{CardPublicId}.</summary>
    [Column("CardPublicId")]
    public string? CardPublicId { get; set; }

    [Column("CardValidUntil")]
    public DateTime? CardValidUntil { get; set; }

    [Column("CardRevokedAt")]
    public DateTime? CardRevokedAt { get; set; }

    [Column("FailedLogins")]
    public int FailedLogins { get; set; }

    [Column("LockedUntil")]
    public DateTime? LockedUntil { get; set; }

    /// <summary>Ausweis gilt: aktives Mitglied, ausgestellt, nicht gesperrt, nicht abgelaufen, mit Lichtbild.</summary>
    public bool IsCardValid(DateTime now) =>
        IsActive && !string.IsNullOrEmpty(CardPublicId) && CardRevokedAt == null
        && (CardValidUntil == null || CardValidUntil.Value.Date >= now.Date)
        && !string.IsNullOrEmpty(PhotoFile);
}


