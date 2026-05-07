using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

/// <summary>
/// Join-Tabelle für die N:N-Beziehung zwischen Mitgliedern und Einheiten.
/// Member.UnitNumber bleibt für Abwärtskompatibilität als "Primäreinheit".
/// In dieser Tabelle stehen ALLE Einheiten eines Mitglieds (inkl. der Primäreinheit).
/// </summary>
[Table("MemberUnit")]
public class MemberUnit
{
    [PrimaryKey(0), NotNull]
    [Column("MemberId")]
    public int MemberId { get; set; }

    [PrimaryKey(1), NotNull]
    [Column("UnitNumber")]
    public int UnitNumber { get; set; }
}
