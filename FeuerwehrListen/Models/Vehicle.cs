using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

[Table("Vehicle")]
public class Vehicle
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }
    
    [Column("Name")]
    public string Name { get; set; } = string.Empty;
    
    [Column("CallSign")]
    public string CallSign { get; set; } = string.Empty;
    
    [Column("Type")]
    public VehicleType Type { get; set; }
    
    [Column("IsActive")]
    public bool IsActive { get; set; }
    
    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }
}

