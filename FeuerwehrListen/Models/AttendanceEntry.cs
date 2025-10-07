using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

[Table("AttendanceEntry")]
public class AttendanceEntry
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }
    
    [Column("AttendanceListId")]
    public int AttendanceListId { get; set; }
    
    [Column("NameOrId")]
    public string NameOrId { get; set; } = string.Empty;
    
    [Column("EnteredAt")]
    public DateTime EnteredAt { get; set; }
}

