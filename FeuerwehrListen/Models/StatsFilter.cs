namespace FeuerwehrListen.Models;

/// <summary>Listentyp-Auswahl für die Statistik.</summary>
public enum StatListType
{
    Operation = 0,        // Einsatzlisten
    Attendance = 1,       // Anwesenheitslisten
    FireSafetyWatch = 2,  // Brandsicherheitswachen
    All = 3               // Gesamt
}

/// <summary>Filter für alle Statistik-Auswertungen (Typ, Zeitraum, Einheit).</summary>
public class StatsFilter
{
    public StatListType ListType { get; set; } = StatListType.Operation;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Unit { get; set; } = 0; // 0 = alle Einheiten

    public bool IncludeOperations => ListType is StatListType.Operation or StatListType.All;
    public bool IncludeAttendance => ListType is StatListType.Attendance or StatListType.All;
    public bool IncludeFireSafetyWatch => ListType is StatListType.FireSafetyWatch or StatListType.All;

    public string ListTypeLabel => ListType switch
    {
        StatListType.Operation => "Einsatzlisten",
        StatListType.Attendance => "Anwesenheitslisten",
        StatListType.FireSafetyWatch => "Brandsicherheitswachen",
        _ => "Gesamt"
    };
}
