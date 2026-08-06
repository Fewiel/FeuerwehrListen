using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

/// <summary>
/// Eine von einem Termin belegte Ressource (Fahrzeug oder Raum) samt eigenem
/// Freigabestatus. Dadurch kann ein Termin Fahrzeug UND Raum belegen, jeweils mit
/// eigenem Zustaendigen.
/// </summary>
[Table("CalendarEventResource")]
public class CalendarEventResource
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }

    [Column("CalendarEventId")]
    public int CalendarEventId { get; set; }

    [Column("ResourceKind")]
    public CalendarResourceKind ResourceKind { get; set; }

    [Column("ResourceId")]
    public int ResourceId { get; set; }

    [Column("Status")]
    public CalendarResourceStatus Status { get; set; } = CalendarResourceStatus.NichtErforderlich;

    /// <summary>Einmal-Token fuer /approve/{token}. Kryptografisch erzeugt, nicht per Guid.</summary>
    [Column("ApprovalToken")]
    public string? ApprovalToken { get; set; }

    [Column("TokenExpiresAt")]
    public DateTime? TokenExpiresAt { get; set; }

    [Column("TokenUsedAt")]
    public DateTime? TokenUsedAt { get; set; }

    [Column("ApprovedBy")]
    public string? ApprovedBy { get; set; }

    [Column("ApprovedAt")]
    public DateTime? ApprovedAt { get; set; }

    [Column("DecisionComment")]
    public string? DecisionComment { get; set; }
}
