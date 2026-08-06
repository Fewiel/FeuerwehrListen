using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

[Table("Vehicle")]
public class Vehicle
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }
    
    [Column("Name")]
    public string Name { get; set; } = string.Empty;
    
    [Column("CallSign")]
    public string CallSign { get; set; } = string.Empty;
    
    [Column("Type")]
    public VehicleType Type { get; set; }
    
    [Column("IsActive")]
    public bool IsActive { get; set; }

    /// <summary>Beim Eintragen in Einsatzlisten/Berichten anbieten. Default true.</summary>
    [Column("ShowInOperations")]
    public bool ShowInOperations { get; set; } = true;

    /// <summary>Im Kalender buchbar. Default true.</summary>
    [Column("IsBookable")]
    public bool IsBookable { get; set; } = true;

    /// <summary>Kalenderbuchung dieses Fahrzeugs braucht eine Freigabe.</summary>
    [Column("RequiresApproval")]
    public bool RequiresApproval { get; set; }

    /// <summary>Zustaendige fuer die Freigabe - eine oder mehrere Adressen, kommagetrennt.</summary>
    [Column("ApproverEmails")]
    public string? ApproverEmails { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }
}

