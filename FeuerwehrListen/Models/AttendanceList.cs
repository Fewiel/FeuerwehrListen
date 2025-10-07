using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

[Table("AttendanceList")]
public class AttendanceList
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }
    
    [Column("Title")]
    public string Title { get; set; } = string.Empty;
    
    [Column("Unit")]
    public string Unit { get; set; } = string.Empty;
    
    [Column("Description")]
    public string Description { get; set; } = string.Empty;
    
    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }
    
    [Column("Status")]
    public ListStatus Status { get; set; }
    
    [Column("ClosedAt")]
    public DateTime? ClosedAt { get; set; }
    
    [Column("IsArchived")]
    public bool IsArchived { get; set; }
}

