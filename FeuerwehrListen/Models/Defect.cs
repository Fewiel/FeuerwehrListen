using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

[Table("Defect")]
public class Defect
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }

    [Column("Description")]
    public string Description { get; set; } = string.Empty;

    [Column("VehicleId")]
    public int? VehicleId { get; set; }

    [Column("VehicleName")]
    public string? VehicleName { get; set; }

    [Column("CustomVehicle")]
    public string? CustomVehicle { get; set; }

    [Column("Status")]
    public DefectStatus Status { get; set; } = DefectStatus.Open;

    [Column("ReportedByMemberId")]
    public int? ReportedByMemberId { get; set; }

    [Column("ReportedByName")]
    public string ReportedByName { get; set; } = string.Empty;

    [Column("ReportedAt")]
    public DateTime ReportedAt { get; set; } = DateTime.Now;

    [Column("ResolvedByMemberId")]
    public int? ResolvedByMemberId { get; set; }

    [Column("ResolvedByName")]
    public string? ResolvedByName { get; set; }

    [Column("ResolvedAt")]
    public DateTime? ResolvedAt { get; set; }
}

[Table("DefectStatusChange")]
public class DefectStatusChange
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }

    [Column("DefectId")]
    public int DefectId { get; set; }

    [Column("OldStatus")]
    public DefectStatus OldStatus { get; set; }

    [Column("NewStatus")]
    public DefectStatus NewStatus { get; set; }

    [Column("ChangedByName")]
    public string ChangedByName { get; set; } = string.Empty;

    [Column("ChangedAt")]
    public DateTime ChangedAt { get; set; } = DateTime.Now;

    [Column("Comment")]
    public string? Comment { get; set; }
}
