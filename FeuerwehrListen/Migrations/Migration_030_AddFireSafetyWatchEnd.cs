using FluentMigrator;

namespace FeuerwehrListen.Migrations;

/// <summary>
/// Ende einer Brandsicherheitswache. Bisher gab es nur EventDateTime (Beginn) - damit
/// liess sich nicht bestimmen, wie lange ein Fahrzeug durch eine Wache belegt ist.
///
/// Bewusst nullable: Bestandswachen behalten kein Ende und fallen auf die einstellbare
/// Standarddauer (Calendar.FireSafetyWatchDefaultHours) zurueck.
/// </summary>
[Migration(30)]
public class Migration_030_AddFireSafetyWatchEnd : Migration
{
    public override void Up()
    {
        Alter.Table("fire_safety_watches")
            .AddColumn("EndDateTime").AsDateTime().Nullable();
    }

    public override void Down()
    {
        Delete.Column("EndDateTime").FromTable("fire_safety_watches");
    }
}
