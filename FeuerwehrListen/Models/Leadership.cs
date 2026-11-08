using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

/// <summary>Stufen des Fuehrungskreises. Der kleine Kreis steht UEBER dem grossen:
/// sichtbar ist ein Eintrag, wenn die eigene Stufe mindestens die des Eintrags ist.</summary>
public static class LeadershipCircles
{
    public const int None = 0;
    public const int Large = 1;
    public const int Small = 2;

    public static string Label(int circle) => circle switch
    {
        Small => "Kleiner Führungskreis",
        Large => "Großer Führungskreis",
        _ => "—"
    };

    public static bool IsValid(int circle) => circle == Large || circle == Small;
}

public enum LeadershipProtocolStatus
{
    Vorbereitung = 1,
    Laeuft = 2,
    Abgeschlossen = 3
}

public enum LeadershipItemStatus
{
    Offen = 1,
    Besprochen = 2,
    Vertagt = 3,
    /// <summary>Von der Protokollfuehrung abgelehnt - bleibt sichtbar, wird nicht behandelt/uebertragen.</summary>
    Abgelehnt = 4
}

public enum LeadershipTaskStatus
{
    Offen = 1,
    Erledigt = 2,
    InArbeit = 3
}

/// <summary>Protokoll einer Fuehrungskreis-Sitzung (1:1 zum Kalendertermin).</summary>
[Table("LeadershipProtocol")]
public class LeadershipProtocol
{
    [PrimaryKey, Identity, Column("Id")] public int Id { get; set; }
    [Column("CalendarEventId")] public int CalendarEventId { get; set; }
    [Column("Circle")] public int Circle { get; set; }
    [Column("Status")] public LeadershipProtocolStatus Status { get; set; } = LeadershipProtocolStatus.Vorbereitung;
    [Column("AttendanceListId")] public int? AttendanceListId { get; set; }
    /// <summary>Protokollfuehrung: darf Punkte ablehnen/sortieren und die Sitzung beenden.</summary>
    [Column("KeeperMemberId")] public int? KeeperMemberId { get; set; }
    [Column("StartedAt")] public DateTime? StartedAt { get; set; }
    [Column("ClosedAt")] public DateTime? ClosedAt { get; set; }
    [Column("ClosedBy")] public string? ClosedBy { get; set; }
    [Column("CreatedAt")] public DateTime CreatedAt { get; set; }
}

/// <summary>Tagesordnungspunkt. ProtocolId = null: wartet auf die naechste Sitzung des Kreises.</summary>
[Table("LeadershipItem")]
public class LeadershipItem
{
    [PrimaryKey, Identity, Column("Id")] public int Id { get; set; }
    [Column("ProtocolId")] public int? ProtocolId { get; set; }
    [Column("Circle")] public int Circle { get; set; }
    [Column("SortOrder")] public int SortOrder { get; set; }
    [Column("Title")] public string Title { get; set; } = string.Empty;
    [Column("OwnerMemberId")] public int? OwnerMemberId { get; set; }
    [Column("Notes")] public string Notes { get; set; } = string.Empty;
    [Column("Decision")] public string Decision { get; set; } = string.Empty;
    [Column("Status")] public LeadershipItemStatus Status { get; set; } = LeadershipItemStatus.Offen;
    [Column("SourceTaskId")] public int? SourceTaskId { get; set; }
    [Column("CreatedAt")] public DateTime CreatedAt { get; set; }
}

[Table("LeadershipTask")]
public class LeadershipTask
{
    [PrimaryKey, Identity, Column("Id")] public int Id { get; set; }
    [Column("Circle")] public int Circle { get; set; }
    [Column("Title")] public string Title { get; set; } = string.Empty;
    [Column("Description")] public string Description { get; set; } = string.Empty;
    [Column("AssigneeMemberId")] public int? AssigneeMemberId { get; set; }
    [Column("DueDate")] public DateTime? DueDate { get; set; }
    [Column("Status")] public LeadershipTaskStatus Status { get; set; } = LeadershipTaskStatus.Offen;
    [Column("SourceItemId")] public int? SourceItemId { get; set; }
    /// <summary>Notizen der/des Verantwortlichen (auch Nicht-Fuehrungskreis im Mitgliedsbereich).</summary>
    [Column("Notes")] public string Notes { get; set; } = string.Empty;
    [Column("CreatedBy")] public string CreatedBy { get; set; } = string.Empty;
    [Column("CreatedAt")] public DateTime CreatedAt { get; set; }
    [Column("CompletedAt")] public DateTime? CompletedAt { get; set; }
}
