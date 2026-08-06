using FluentMigrator;

namespace FeuerwehrListen.Migrations;

/// <summary>
/// Fahrzeug-Flags fuer den Kalender: Fahrzeuge koennen kalender-only sein (nicht in
/// Einsatzlisten waehlbar) und optional eine Freigabe je Buchung verlangen.
/// WICHTIG: ShowInOperations/IsBookable haben Default TRUE, damit bestehende Fahrzeuge
/// nach der Migration unveraendert weiterlaufen.
/// </summary>
[Migration(26)]
public class Migration_026_AddVehicleBookingFlags : Migration
{
    public override void Up()
    {
        Alter.Table("Vehicle")
            .AddColumn("ShowInOperations").AsBoolean().NotNullable().WithDefaultValue(true)
            .AddColumn("IsBookable").AsBoolean().NotNullable().WithDefaultValue(true)
            .AddColumn("RequiresApproval").AsBoolean().NotNullable().WithDefaultValue(false)
            .AddColumn("ApproverEmails").AsString(500).Nullable();
    }

    public override void Down()
    {
        Delete.Column("ApproverEmails").FromTable("Vehicle");
        Delete.Column("RequiresApproval").FromTable("Vehicle");
        Delete.Column("IsBookable").FromTable("Vehicle");
        Delete.Column("ShowInOperations").FromTable("Vehicle");
    }
}
