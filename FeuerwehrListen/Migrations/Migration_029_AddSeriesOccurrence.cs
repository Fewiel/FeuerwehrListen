using FluentMigrator;

namespace FeuerwehrListen.Migrations;

/// <summary>
/// Merkt sich je materialisiertem Serientermin den urspruenglich berechneten Slot.
///
/// Ohne diese Spalte wird ein einzeln VERSCHOBENER Termin beim naechsten Auffuellen der
/// Serie doppelt: die Materialisierung sucht nach der Startzeit, findet den verschobenen
/// Termin nicht mehr und legt den Original-Slot erneut an.
/// </summary>
[Migration(29)]
public class Migration_029_AddSeriesOccurrence : Migration
{
    public override void Up()
    {
        Alter.Table("CalendarEvent")
            .AddColumn("SeriesOccurrence").AsDateTime().Nullable();

        // Bestandsdaten: bisher entspricht der Slot der Startzeit.
        Execute.Sql("UPDATE \"CalendarEvent\" SET \"SeriesOccurrence\" = \"StartTime\" WHERE \"SeriesId\" IS NOT NULL");
    }

    public override void Down()
    {
        Delete.Column("SeriesOccurrence").FromTable("CalendarEvent");
    }
}
