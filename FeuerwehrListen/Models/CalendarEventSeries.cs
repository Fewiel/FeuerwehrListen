using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

/// <summary>
/// Serienregel. Die einzelnen Termine werden daraus in einem rollierenden Horizont
/// als CalendarEvent-Zeilen materialisiert (nicht zur Laufzeit berechnet), damit
/// Ausnahmen und Konfliktpruefung ohne Sonderlogik funktionieren.
/// </summary>
[Table("CalendarEventSeries")]
public class CalendarEventSeries
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }

    [Column("Type")]
    public CalendarEventType Type { get; set; } = CalendarEventType.Dienst;

    [Column("Title"), NotNull]
    public string Title { get; set; } = string.Empty;

    [Column("Description")]
    public string? Description { get; set; }

    [Column("Location")]
    public string? Location { get; set; }

    [Column("UnitNumber")]
    public int? UnitNumber { get; set; }

    [Column("Frequency")]
    public CalendarFrequency Frequency { get; set; } = CalendarFrequency.Woechentlich;

    /// <summary>Bitmaske der Wochentage: Sonntag=1, Montag=2, Dienstag=4 ... Samstag=64.</summary>
    [Column("WeekdayMask")]
    public int WeekdayMask { get; set; }

    /// <summary>Nur bei monatlicher Wiederholung.</summary>
    [Column("DayOfMonth")]
    public int? DayOfMonth { get; set; }

    /// <summary>Startzeit als Minuten seit Mitternacht.</summary>
    [Column("StartMinuteOfDay")]
    public int StartMinuteOfDay { get; set; }

    [Column("DurationMinutes")]
    public int DurationMinutes { get; set; } = 120;

    [Column("SeriesStart")]
    public DateTime SeriesStart { get; set; }

    /// <summary>Null = offene Serie.</summary>
    [Column("SeriesEnd")]
    public DateTime? SeriesEnd { get; set; }

    [Column("MinutesBeforeEvent")]
    public int MinutesBeforeEvent { get; set; } = 60;

    [Column("RequestedBy"), NotNull]
    public string RequestedBy { get; set; } = string.Empty;

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }
}
