using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

/// <summary>
/// An welcher Stelle der Stärke-Notation (Führer / Helfer / Mannschaft / Gesamt)
/// eine Funktion gezählt wird.
/// </summary>
public enum StrengthPosition
{
    Zugfuehrer = 1,      // 1. Zahl (Führer)
    Gruppenfuehrer = 2,  // 2. Zahl (Helfer/Unterführer)
    Mannschaft = 3       // 3. Zahl (Mannschaft)
}

[Table("OperationFunctionDef")]
public class OperationFunctionDef
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }

    [Column("Name"), NotNull]
    public string Name { get; set; } = string.Empty;

    [Column("IsDefault")]
    public bool IsDefault { get; set; }

    /// <summary>Zählstelle in der Stärke (Standard: Mannschaft).</summary>
    [Column("StrengthPosition")]
    public StrengthPosition StrengthPosition { get; set; } = StrengthPosition.Mannschaft;
}


