using FluentMigrator;

namespace FeuerwehrListen.Migrations;

/// <summary>
/// Kalender: Termine (Dienste, Veranstaltungen, Fahrzeug-/Raumbuchungen), Serien mit
/// Ausnahmen und die belegten Ressourcen je Termin.
/// Brandsicherheitswachen werden NICHT hier gespeichert, sondern aus fire_safety_watches
/// in den Kalender projiziert (keine Datendopplung).
/// Bewusst ohne .ForeignKey(): das bestehende Muster aus Migration 009 zeigt auf nicht
/// existierende Tabellen und wuerde unter MySQL brechen. Stattdessen benannte Indizes.
/// </summary>
[Migration(28)]
public class Migration_028_AddCalendar : Migration
{
    public override void Up()
    {
        // Serienregel (Vorlage). Einzeltermine werden daraus materialisiert.
        Create.Table("CalendarEventSeries")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Type").AsInt32().NotNullable().WithDefaultValue(1)
            .WithColumn("Title").AsString(200).NotNullable()
            .WithColumn("Description").AsString(2000).Nullable()
            .WithColumn("Location").AsString(200).Nullable()
            .WithColumn("UnitNumber").AsInt32().Nullable()
            // 1=woechentlich, 2=zweiwoechentlich, 3=monatlich
            .WithColumn("Frequency").AsInt32().NotNullable().WithDefaultValue(1)
            // Bitmaske der Wochentage: Sonntag=1, Montag=2, Dienstag=4 ... Samstag=64
            .WithColumn("WeekdayMask").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("DayOfMonth").AsInt32().Nullable()
            // Minuten seit Mitternacht - vermeidet TimeOnly (im Datenmodell nirgends genutzt)
            .WithColumn("StartMinuteOfDay").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("DurationMinutes").AsInt32().NotNullable().WithDefaultValue(120)
            .WithColumn("SeriesStart").AsDateTime().NotNullable()
            .WithColumn("SeriesEnd").AsDateTime().Nullable()
            .WithColumn("MinutesBeforeEvent").AsInt32().NotNullable().WithDefaultValue(60)
            .WithColumn("RequestedBy").AsString(200).NotNullable().WithDefaultValue("")
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

        Create.Table("CalendarEvent")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            // 1=Dienst, 2=Veranstaltung, 3=Fahrzeugbuchung, 4=Raumbuchung
            .WithColumn("Type").AsInt32().NotNullable().WithDefaultValue(2)
            .WithColumn("Title").AsString(200).NotNullable()
            .WithColumn("Description").AsString(2000).Nullable()
            .WithColumn("Location").AsString(200).Nullable()
            .WithColumn("StartTime").AsDateTime().NotNullable()
            .WithColumn("EndTime").AsDateTime().NotNullable()
            .WithColumn("IsAllDay").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("UnitNumber").AsInt32().Nullable()
            // 1=Angefragt, 2=Bestaetigt, 3=Abgelehnt, 4=Storniert
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(1)
            // Anonymes Buchen erlaubt, aber Identifikation ist Pflicht (Name ODER Mitgliedsnummer)
            .WithColumn("RequestedBy").AsString(200).NotNullable().WithDefaultValue("")
            .WithColumn("RequestedByEmail").AsString(320).Nullable()
            .WithColumn("MemberId").AsInt32().Nullable()
            .WithColumn("SeriesId").AsInt32().Nullable()
            // Einmal einzeln geaendert -> wird von der Serien-Materialisierung nicht mehr angefasst
            .WithColumn("IsSeriesException").AsBoolean().NotNullable().WithDefaultValue(false)
            // Rueckverweis auf die automatisch erzeugte Anwesenheitsliste (idempotenter Duplikatschutz)
            .WithColumn("AttendanceListId").AsInt32().Nullable()
            .WithColumn("MinutesBeforeEvent").AsInt32().NotNullable().WithDefaultValue(60)
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

        Create.Index("IX_CalendarEvent_StartTime").OnTable("CalendarEvent").OnColumn("StartTime");
        Create.Index("IX_CalendarEvent_SeriesId").OnTable("CalendarEvent").OnColumn("SeriesId");

        // Belegte Ressourcen je Termin. Ein Dienst, der Fahrzeuge blockt, ist einfach ein
        // Termin mit Ressourcen-Zeilen - die normale Ueberschneidungspruefung greift dann
        // automatisch und verhindert eine Buchung.
        Create.Table("CalendarEventResource")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("CalendarEventId").AsInt32().NotNullable()
            // 1=Fahrzeug, 2=Raum
            .WithColumn("ResourceKind").AsInt32().NotNullable()
            .WithColumn("ResourceId").AsInt32().NotNullable()
            // 1=NichtErforderlich, 2=Angefragt, 3=Freigegeben, 4=Abgelehnt
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(1)
            .WithColumn("ApprovalToken").AsString(128).Nullable()
            .WithColumn("TokenExpiresAt").AsDateTime().Nullable()
            .WithColumn("TokenUsedAt").AsDateTime().Nullable()
            .WithColumn("ApprovedBy").AsString(200).Nullable()
            .WithColumn("ApprovedAt").AsDateTime().Nullable()
            .WithColumn("DecisionComment").AsString(500).Nullable();

        Create.Index("IX_CalendarEventResource_CalendarEventId").OnTable("CalendarEventResource").OnColumn("CalendarEventId");
        Create.Index("IX_CalendarEventResource_ApprovalToken").OnTable("CalendarEventResource").OnColumn("ApprovalToken");
        Create.Index("IX_CalendarEventResource_Resource").OnTable("CalendarEventResource")
            .OnColumn("ResourceKind").Ascending().OnColumn("ResourceId").Ascending();
    }

    public override void Down()
    {
        Delete.Table("CalendarEventResource");
        Delete.Table("CalendarEvent");
        Delete.Table("CalendarEventSeries");
    }
}
