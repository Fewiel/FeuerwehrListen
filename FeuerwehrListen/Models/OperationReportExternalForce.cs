using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

/// <summary>
/// Extern hinzugezogene Kraft (Fahrzeug/Einheit) zu einem Einsatzbericht.
/// Wird über die Kräfteerfassung im Bericht als eigene, bearbeitbare Liste geführt.
/// </summary>
[Table("OperationReportExternalForce")]
public class OperationReportExternalForce
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }

    [Column("OperationReportId"), NotNull]
    public int OperationReportId { get; set; }

    /// <summary>Funk-Rufname der Einheit, z. B. "Florian Coesfeld 1/44".</summary>
    [Column("Rufname")] public string? Rufname { get; set; }

    /// <summary>Stärke, z. B. "1/8" oder "0/1/6/7".</summary>
    [Column("Staerke")] public string? Staerke { get; set; }
}
