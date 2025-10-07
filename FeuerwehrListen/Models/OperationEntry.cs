using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

[Table("OperationEntry")]
public class OperationEntry
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }
    
    [Column("OperationListId")]
    public int OperationListId { get; set; }
    
    [Column("NameOrId")]
    public string NameOrId { get; set; } = string.Empty;
    
    [Column("Vehicle")]
    public string Vehicle { get; set; } = string.Empty;
    
    [Column("Function")]
    public OperationFunction Function { get; set; }
    
    [Column("WithBreathingApparatus")]
    public bool WithBreathingApparatus { get; set; }
    
    [Column("EnteredAt")]
    public DateTime EnteredAt { get; set; }
}

