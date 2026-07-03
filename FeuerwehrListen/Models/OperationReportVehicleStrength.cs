using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

/// <summary>
/// Vorab/erfasste Stärke je Fahrzeug zu einem Einsatzbericht (Format „Führer/Mannschaft", z. B. 1/8).
/// Wird beim Abschließen der Einsatzliste ggf. aus den tatsächlichen Einträgen nachkorrigiert.
/// </summary>
[Table("OperationReportVehicleStrength")]
public class OperationReportVehicleStrength
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }

    [Column("OperationReportId"), NotNull]
    public int OperationReportId { get; set; }

    [Column("VehicleName")]
    public string? VehicleName { get; set; }

    [Column("Staerke")]
    public string? Staerke { get; set; }
}
