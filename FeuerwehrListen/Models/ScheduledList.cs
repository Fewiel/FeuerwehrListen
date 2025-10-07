using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

[Table("ScheduledList")]
public class ScheduledList
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }
    
    [Column("Type")]
    public ScheduledListType Type { get; set; }
    
    [Column("Title")]
    public string Title { get; set; } = string.Empty;
    
    [Column("Unit")]
    public string Unit { get; set; } = string.Empty;
    
    [Column("Description")]
    public string Description { get; set; } = string.Empty;
    
    [Column("OperationNumber")]
    public string OperationNumber { get; set; } = string.Empty;
    
    [Column("Keyword")]
    public string Keyword { get; set; } = string.Empty;
    
    [Column("ScheduledEventTime")]
    public DateTime ScheduledEventTime { get; set; }
    
    [Column("MinutesBeforeEvent")]
    public int MinutesBeforeEvent { get; set; }
    
    [Column("IsProcessed")]
    public bool IsProcessed { get; set; }
    
    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }
}

