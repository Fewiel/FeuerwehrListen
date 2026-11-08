using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

/// <summary>Im Voraus gemeldete Abwesenheit: jede Anwesenheitsliste, deren Zeitraum sich mit
/// From..To ueberschneidet, bekommt das Mitglied automatisch als "entschuldigt". Nicht fuer Einsaetze.</summary>
[Table("MemberAbsence")]
public class MemberAbsence
{
    [PrimaryKey, Identity, Column("Id")] public int Id { get; set; }
    [Column("MemberId")] public int MemberId { get; set; }
    [Column("FromTime")] public DateTime From { get; set; }
    [Column("ToTime")] public DateTime To { get; set; }
    [Column("Note")] public string Note { get; set; } = string.Empty;
    [Column("CreatedBy")] public string CreatedBy { get; set; } = string.Empty;
    [Column("CreatedAt")] public DateTime CreatedAt { get; set; }
}
