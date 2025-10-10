using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

[Table("OperationList")]
public class OperationList
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }
    
    [Column("OperationNumber")]
    public string OperationNumber { get; set; } = string.Empty;
    
    [Column("Keyword")]
    public string Keyword { get; set; } = string.Empty;
    
    [Column("KeywordId")]
    public int? KeywordId { get; set; }
    
    [Column("AlertTime")]
    public DateTime AlertTime { get; set; }
    
    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }
    
    [Column("Status")]
    public ListStatus Status { get; set; }
    
    [Column("ClosedAt")]
    public DateTime? ClosedAt { get; set; }
    
    [Column("IsArchived")]
    public bool IsArchived { get; set; }

    [Column("Address")]
    public string? Address { get; set; }

    [Column("Latitude")]
    public double? Latitude { get; set; }

    [Column("Longitude")]
    public double? Longitude { get; set; }
}

