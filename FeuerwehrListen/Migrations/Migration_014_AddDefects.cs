using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(14)]
public class Migration_014_AddDefects : Migration
{
    public override void Up()
    {
        Create.Table("Defect")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Description").AsString(2000).NotNullable()
            .WithColumn("VehicleId").AsInt32().Nullable()
            .WithColumn("VehicleName").AsString(200).Nullable()
            .WithColumn("CustomVehicle").AsString(200).Nullable()
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(1)
            .WithColumn("ReportedByMemberId").AsInt32().Nullable()
            .WithColumn("ReportedByName").AsString(200).NotNullable()
            .WithColumn("ReportedAt").AsDateTime().NotNullable()
            .WithColumn("ResolvedByMemberId").AsInt32().Nullable()
            .WithColumn("ResolvedByName").AsString(200).Nullable()
            .WithColumn("ResolvedAt").AsDateTime().Nullable();

        Create.Table("DefectStatusChange")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("DefectId").AsInt32().NotNullable()
            .WithColumn("OldStatus").AsInt32().NotNullable()
            .WithColumn("NewStatus").AsInt32().NotNullable()
            .WithColumn("ChangedByName").AsString(200).NotNullable()
            .WithColumn("ChangedAt").AsDateTime().NotNullable()
            .WithColumn("Comment").AsString(1000).Nullable();
    }

    public override void Down()
    {
        Delete.Table("DefectStatusChange");
        Delete.Table("Defect");
    }
}
