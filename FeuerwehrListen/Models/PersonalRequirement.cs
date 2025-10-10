using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

[Table("PersonalRequirement")]
public class PersonalRequirement
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }
    
    [Column("KeywordId"), NotNull]
    public int KeywordId { get; set; }
    
    [Column("FunctionDefId"), NotNull]
    public int FunctionDefId { get; set; }
    
    [Column("MinimumCount"), NotNull]
    public int MinimumCount { get; set; } = 1;
    
    [Column("IsRequired")]
    public bool IsRequired { get; set; } = true;
    
    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
