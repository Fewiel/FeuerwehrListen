using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

[Table("OperationFunctionDef")]
public class OperationFunctionDef
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }

    [Column("Name"), NotNull]
    public string Name { get; set; } = string.Empty;

    // Markiert, ob diese Funktion initial/empfohlen ist (z.B. Atemschutz, Gruppenführer, Maschinist)
    [Column("IsDefault")]
    public bool IsDefault { get; set; }
}


