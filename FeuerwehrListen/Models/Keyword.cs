using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

[Table("Keyword")]
public class Keyword
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }
    
    [Column("Name"), NotNull]
    public string Name { get; set; } = string.Empty;
    
    [Column("Description")]
    public string? Description { get; set; }
    
    [Column("IsActive")]
    public bool IsActive { get; set; } = true;
    
    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
