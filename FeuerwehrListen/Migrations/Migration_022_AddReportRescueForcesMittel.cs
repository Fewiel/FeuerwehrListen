using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(22)]
public class Migration_022_AddReportRescueForcesMittel : Migration
{
    public override void Up()
    {
        // Menschenrettung (Ja/Nein + Dauer + Personalaufwand) am Einsatzbericht
        Alter.Table("OperationReport")
            .AddColumn("HatMenschenrettung").AsBoolean().NotNullable().WithDefaultValue(false)
            .AddColumn("MenschenrettungDauer").AsString(100).Nullable()
            .AddColumn("MenschenrettungPersonalaufwand").AsString(100).Nullable();

        // Externe Kräfte (eigene, bearbeitbare Liste)
        Create.Table("OperationReportExternalForce")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("OperationReportId").AsInt32().NotNullable()
            .WithColumn("Rufname").AsString(200).Nullable()
            .WithColumn("Staerke").AsString(100).Nullable();

        Create.Index("IX_OperationReportExternalForce_ReportId")
            .OnTable("OperationReportExternalForce")
            .OnColumn("OperationReportId");

        // Eingesetzte Mittel (vordefiniert + Sonstiges)
        Create.Table("OperationReportMittel")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("OperationReportId").AsInt32().NotNullable()
            .WithColumn("Name").AsString(200).Nullable()
            .WithColumn("Anzahl").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("IsCustom").AsBoolean().NotNullable().WithDefaultValue(false);

        Create.Index("IX_OperationReportMittel_ReportId")
            .OnTable("OperationReportMittel")
            .OnColumn("OperationReportId");
    }

    public override void Down()
    {
        Delete.Table("OperationReportMittel");
        Delete.Table("OperationReportExternalForce");
        Delete.Column("HatMenschenrettung").FromTable("OperationReport");
        Delete.Column("MenschenrettungDauer").FromTable("OperationReport");
        Delete.Column("MenschenrettungPersonalaufwand").FromTable("OperationReport");
    }
}
