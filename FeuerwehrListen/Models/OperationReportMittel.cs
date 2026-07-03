using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

/// <summary>
/// Ein bei einem Einsatz eingesetztes Mittel (Gerät/Material) zu einem Einsatzbericht.
/// Vordefinierte Mittel (IsCustom=false) stammen aus der festen Liste (Anlage Kostenersatz),
/// freie "Sonstiges"-Einträge haben IsCustom=true.
/// </summary>
[Table("OperationReportMittel")]
public class OperationReportMittel
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }

    [Column("OperationReportId"), NotNull]
    public int OperationReportId { get; set; }

    [Column("Name")] public string? Name { get; set; }

    [Column("Anzahl")] public int Anzahl { get; set; }

    [Column("IsCustom")] public bool IsCustom { get; set; }
}
