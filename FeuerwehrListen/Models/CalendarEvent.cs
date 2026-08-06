using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

/// <summary>
/// Ein Termin im Kalender. Serientermine werden als einzelne Zeilen materialisiert
/// (siehe CalendarEventSeries) - dadurch funktionieren Ausnahmen und die
/// Ueberschneidungspruefung ohne Sonderlogik.
/// </summary>
[Table("CalendarEvent")]
public class CalendarEvent
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }

    [Column("Type")]
    public CalendarEventType Type { get; set; } = CalendarEventType.Veranstaltung;

    [Column("Title"), NotNull]
    public string Title { get; set; } = string.Empty;

    [Column("Description")]
    public string? Description { get; set; }

    [Column("Location")]
    public string? Location { get; set; }

    [Column("StartTime")]
    public DateTime StartTime { get; set; }

    [Column("EndTime")]
    public DateTime EndTime { get; set; }

    [Column("IsAllDay")]
    public bool IsAllDay { get; set; }

    [Column("UnitNumber")]
    public int? UnitNumber { get; set; }

    [Column("Status")]
    public CalendarEventStatus Status { get; set; } = CalendarEventStatus.Angefragt;

    /// <summary>Name oder Mitgliedsnummer des Antragstellers (Pflicht, auch ohne Login).</summary>
    [Column("RequestedBy"), NotNull]
    public string RequestedBy { get; set; } = string.Empty;

    [Column("RequestedByEmail")]
    public string? RequestedByEmail { get; set; }

    /// <summary>Gesetzt, wenn der Antragsteller als Mitglied aufgeloest werden konnte.</summary>
    [Column("MemberId")]
    public int? MemberId { get; set; }

    [Column("SeriesId")]
    public int? SeriesId { get; set; }

    /// <summary>
    /// Urspruenglich von der Serienregel berechneter Slot. Bleibt beim Verschieben eines
    /// Einzeltermins unveraendert - nur dadurch erkennt die Materialisierung, dass dieser
    /// Slot bereits belegt ist, und erzeugt ihn nicht ein zweites Mal.
    /// </summary>
    [Column("SeriesOccurrence")]
    public DateTime? SeriesOccurrence { get; set; }

    /// <summary>Einzeln geaendert -> von der Serien-Materialisierung nicht mehr anfassen.</summary>
    [Column("IsSeriesException")]
    public bool IsSeriesException { get; set; }

    /// <summary>Rueckverweis auf die erzeugte Anwesenheitsliste - dient zugleich als
    /// idempotenter Duplikatschutz im Hintergrunddienst.</summary>
    [Column("AttendanceListId")]
    public int? AttendanceListId { get; set; }

    /// <summary>Vorlauf, mit dem die Anwesenheitsliste vor Beginn erzeugt wird (nur Dienst).</summary>
    [Column("MinutesBeforeEvent")]
    public int MinutesBeforeEvent { get; set; } = 60;

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }
}
