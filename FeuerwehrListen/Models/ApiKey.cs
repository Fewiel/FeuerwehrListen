using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

[Table("ApiKey")]
public class ApiKey
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }
    
    [Column("Key")]
    public string Key { get; set; } = string.Empty;
    
    [Column("Description")]
    public string Description { get; set; } = string.Empty;
    
    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }
    
    [Column("IsActive")]
    public bool IsActive { get; set; }
}

