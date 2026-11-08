using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

/// <summary>Web-Push-Abo eines Mitglieds (ein Eintrag je Geraet/Browser).</summary>
[Table("PushSubscription")]
public class MemberPushSubscription
{
    [PrimaryKey, Identity, Column("Id")] public int Id { get; set; }
    [Column("MemberId")] public int MemberId { get; set; }
    [Column("Endpoint")] public string Endpoint { get; set; } = string.Empty;
    [Column("P256dh")] public string P256dh { get; set; } = string.Empty;
    [Column("Auth")] public string Auth { get; set; } = string.Empty;
    [Column("CreatedAt")] public DateTime CreatedAt { get; set; }
}
