using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

[Table("Member")]
public class Member
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }
    
    [Column("MemberNumber")]
    public string MemberNumber { get; set; } = string.Empty;
    
    [Column("FirstName")]
    public string FirstName { get; set; } = string.Empty;
    
    [Column("LastName")]
    public string LastName { get; set; } = string.Empty;
    
    [Column("IsActive")]
    public bool IsActive { get; set; }
    
    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }
}


