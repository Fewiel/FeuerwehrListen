using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

/// <summary>Buchbarer Raum (Schulungsraum, Fahrzeughalle, ...).</summary>
[Table("Room")]
public class Room
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }

    [Column("Name"), NotNull]
    public string Name { get; set; } = string.Empty;

    [Column("Description")]
    public string? Description { get; set; }

    [Column("Capacity")]
    public int? Capacity { get; set; }

    /// <summary>Buchung dieses Raums braucht eine Bestaetigung.</summary>
    [Column("RequiresApproval")]
    public bool RequiresApproval { get; set; }

    /// <summary>Zustaendige fuer die Freigabe - eine oder mehrere Adressen, kommagetrennt.</summary>
    [Column("ApproverEmails")]
    public string? ApproverEmails { get; set; }

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }
}
