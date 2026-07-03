using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(23)]
public class Migration_023_AddStrengthPositionAndVehicleStrength : Migration
{
    public override void Up()
    {
        // Zählstelle je Funktion (Standard 3 = Mannschaft)
        Alter.Table("OperationFunctionDef")
            .AddColumn("StrengthPosition").AsInt32().NotNullable().WithDefaultValue(3);

        // Stärke je Fahrzeug am Einsatzbericht
        Create.Table("OperationReportVehicleStrength")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("OperationReportId").AsInt32().NotNullable()
            .WithColumn("VehicleName").AsString(200).Nullable()
            .WithColumn("Staerke").AsString(100).Nullable();

        Create.Index("IX_OperationReportVehicleStrength_ReportId")
            .OnTable("OperationReportVehicleStrength")
            .OnColumn("OperationReportId");
    }

    public override void Down()
    {
        Delete.Table("OperationReportVehicleStrength");
        Delete.Column("StrengthPosition").FromTable("OperationFunctionDef");
    }
}
