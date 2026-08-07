using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;
using LinqToDB.Data;

namespace FeuerwehrListen.Repositories;

/// <summary>
/// Zugriff auf Kalendertermine, Serien und belegte Ressourcen.
/// Zeiten sind durchgaengig Lokalzeit (DateTime.Now) - passend zum restlichen Datenmodell.
/// </summary>
public class CalendarRepository
{
    private readonly AppDbConnection _db;

    public CalendarRepository(AppDbConnection db)
    {
        _db = db;
    }

    /// <summary>Termine, die sich mit [from,to) ueberschneiden. Stornierte bleiben aussen vor.</summary>
    public async Task<List<CalendarEvent>> GetEventsInRangeAsync(DateTime from, DateTime to, bool includeCancelled = false)
    {
        var q = _db.CalendarEvents.Where(e => e.StartTime < to && e.EndTime > from);
        if (!includeCancelled)
            q = q.Where(e => e.Status != CalendarEventStatus.Storniert);
        return await q.OrderBy(e => e.StartTime).ToListAsync();
    }

    public async Task<CalendarEvent?> GetEventAsync(int id)
    {
        return await _db.CalendarEvents.FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<int> InsertEventAsync(CalendarEvent e)
    {
        return await _db.InsertWithInt32IdentityAsync(e);
    }

    public async Task UpdateEventAsync(CalendarEvent e)
    {
        await _db.UpdateAsync(e);
    }

    public async Task DeleteEventAsync(int id)
    {
        // Kind-Zeilen zuerst - referenzielle Integritaet wird hier per Hand hergestellt
        // (die App verwendet bewusst keine DB-Fremdschluessel).
        await _db.CalendarEventResources.Where(r => r.CalendarEventId == id).DeleteAsync();
        await _db.CalendarEvents.Where(e => e.Id == id).DeleteAsync();
    }

    // --- Ressourcen ---

    public async Task<List<CalendarEventResource>> GetResourcesForEventAsync(int eventId)
    {
        return await _db.CalendarEventResources.Where(r => r.CalendarEventId == eventId).ToListAsync();
    }

    /// <summary>Bulk-Laden fuer die Monatsansicht - vermeidet N+1.</summary>
    public async Task<List<CalendarEventResource>> GetResourcesForEventsAsync(ICollection<int> eventIds)
    {
        if (eventIds.Count == 0) return new List<CalendarEventResource>();
        return await _db.CalendarEventResources.Where(r => eventIds.Contains(r.CalendarEventId)).ToListAsync();
    }

    public async Task<int> InsertResourceAsync(CalendarEventResource r)
    {
        return await _db.InsertWithInt32IdentityAsync(r);
    }

    public async Task UpdateResourceAsync(CalendarEventResource r)
    {
        await _db.UpdateAsync(r);
    }

    public async Task DeleteResourcesForEventAsync(int eventId)
    {
        await _db.CalendarEventResources.Where(r => r.CalendarEventId == eventId).DeleteAsync();
    }

    /// <summary>
    /// Belegungen, die einer geplanten Buchung im Weg stehen. Ueberschneidung gilt als
    /// start &lt; fremdesEnde UND ende &gt; fremderStart. Abgelehnte/stornierte Termine und
    /// abgelehnte Ressourcenzeilen zaehlen nicht.
    /// Ein Dienst, der Fahrzeuge blockt, faellt hier automatisch mit hinein.
    /// </summary>
    public async Task<List<(CalendarEventResource Resource, CalendarEvent Event)>> GetConflictsAsync(
        CalendarResourceKind kind, int resourceId, DateTime start, DateTime end, int? excludeEventId = null)
    {
        var q =
            from r in _db.CalendarEventResources
            join e in _db.CalendarEvents on r.CalendarEventId equals e.Id
            where r.ResourceKind == kind
                  && r.ResourceId == resourceId
                  && r.Status != CalendarResourceStatus.Abgelehnt
                  && e.Status != CalendarEventStatus.Storniert
                  && e.Status != CalendarEventStatus.Abgelehnt
                  && e.StartTime < end
                  && e.EndTime > start
            select new { r, e };

        if (excludeEventId is int ex)
            q = q.Where(x => x.e.Id != ex);

        var rows = await q.ToListAsync();
        return rows.Select(x => (x.r, x.e)).ToList();
    }

    /// <summary>
    /// Belegungen durch Brandsicherheitswachen. Diese liegen in einer eigenen Tabelle und
    /// haengen ueber die Anforderungen am Fahrzeug - sie tauchen daher NICHT in
    /// CalendarEventResource auf und muessen getrennt geprueft werden.
    ///
    /// Das Ende ist optional; fehlt es (Altdaten), gilt defaultHours ab Beginn. Die
    /// Ueberschneidung wird im Speicher gerechnet, weil LinqToDB die Datumsarithmetik
    /// auf Spalten nicht uebersetzen kann.
    /// </summary>
    public async Task<List<(string WatchName, DateTime Start, DateTime End)>> GetWatchConflictsAsync(
        int vehicleId, DateTime start, DateTime end, int defaultHours)
    {
        // Vorfilter grosszuegig: Beginn darf bis defaultHours vor dem Zeitfenster liegen.
        var earliest = start.AddHours(-Math.Max(defaultHours, 24));

        var rows = await (
            from req in _db.FireSafetyWatchRequirements
            join w in _db.FireSafetyWatches on req.FireSafetyWatchId equals w.Id
            where req.VehicleId == vehicleId
                  && !w.IsArchived
                  && w.EventDateTime < end
                  && w.EventDateTime >= earliest
            select new { w.Name, w.EventDateTime, w.EndDateTime }).ToListAsync();

        var result = new List<(string, DateTime, DateTime)>();
        foreach (var r in rows.DistinctBy(x => new { x.Name, x.EventDateTime }))
        {
            var wEnd = r.EndDateTime ?? r.EventDateTime.AddHours(defaultHours);
            if (r.EventDateTime < end && wEnd > start)
                result.Add((r.Name, r.EventDateTime, wEnd));
        }
        return result;
    }

    /// <summary>
    /// Kalenderbuchungen eines Fahrzeugs im Zeitraum - fuer die Gegenrichtung: beim
    /// Anlegen einer Brandsicherheitswache darf das Fahrzeug nicht schon belegt sein.
    /// </summary>
    public async Task<List<(string Title, DateTime Start, DateTime End)>> GetVehicleBookingsAsync(
        int vehicleId, DateTime start, DateTime end)
    {
        var rows = await (
            from r in _db.CalendarEventResources
            join e in _db.CalendarEvents on r.CalendarEventId equals e.Id
            where r.ResourceKind == CalendarResourceKind.Vehicle
                  && r.ResourceId == vehicleId
                  && r.Status != CalendarResourceStatus.Abgelehnt
                  && e.Status != CalendarEventStatus.Storniert
                  && e.Status != CalendarEventStatus.Abgelehnt
                  && e.StartTime < end
                  && e.EndTime > start
            select new { e.Title, e.StartTime, e.EndTime }).ToListAsync();

        return rows.Select(x => (x.Title, x.StartTime, x.EndTime)).ToList();
    }

    /// <summary>Alle Belegungen im Zeitraum - fuer die Verfuegbarkeitsanzeige im Buchungsdialog.</summary>
    public async Task<List<(CalendarEventResource Resource, CalendarEvent Event)>> GetBookingsInRangeAsync(DateTime rangeStart, DateTime rangeEnd)
    {
        var rows = await (
            from r in _db.CalendarEventResources
            join e in _db.CalendarEvents on r.CalendarEventId equals e.Id
            where r.Status != CalendarResourceStatus.Abgelehnt
                  && e.Status != CalendarEventStatus.Storniert
                  && e.Status != CalendarEventStatus.Abgelehnt
                  && e.StartTime < rangeEnd
                  && e.EndTime > rangeStart
            select new { r, e }).ToListAsync();
        return rows.Select(x => (x.r, x.e)).ToList();
    }

    // --- Freigabe-Token ---

    /// <summary>Ressourcenzeile zu einem Freigabe-Token. Bewusst ohne Statusfilter, damit
    /// der Endpoint zwischen "unbekannt" und "schon entschieden" unterscheiden kann.</summary>
    public async Task<CalendarEventResource?> GetResourceByTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        return await _db.CalendarEventResources.FirstOrDefaultAsync(r => r.ApprovalToken == token);
    }

    // --- Serien ---

    public async Task<List<CalendarEventSeries>> GetAllSeriesAsync()
    {
        return await _db.CalendarEventSeriesSet.OrderBy(s => s.Title).ToListAsync();
    }

    public async Task<List<CalendarEventSeries>> GetActiveSeriesAsync()
    {
        return await _db.CalendarEventSeriesSet.Where(s => s.IsActive).ToListAsync();
    }

    public async Task<CalendarEventSeries?> GetSeriesAsync(int id)
    {
        return await _db.CalendarEventSeriesSet.FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<int> InsertSeriesAsync(CalendarEventSeries s)
    {
        return await _db.InsertWithInt32IdentityAsync(s);
    }

    public async Task UpdateSeriesAsync(CalendarEventSeries s)
    {
        await _db.UpdateAsync(s);
    }

    /// <summary>Alle bereits materialisierten Termine einer Serie ab einem Zeitpunkt.</summary>
    public async Task<List<CalendarEvent>> GetSeriesEventsAsync(int seriesId, DateTime? from = null)
    {
        var q = _db.CalendarEvents.Where(e => e.SeriesId == seriesId);
        if (from is DateTime f) q = q.Where(e => e.StartTime >= f);
        return await q.OrderBy(e => e.StartTime).ToListAsync();
    }

    /// <summary>
    /// Kuenftige Serientermine loeschen, die NICHT als Ausnahme markiert sind.
    /// Ausnahmen (verschoben/geaendert/abgesagt) bleiben bewusst erhalten.
    /// </summary>
    public async Task DeleteFutureSeriesEventsAsync(int seriesId, DateTime from)
    {
        var ids = await _db.CalendarEvents
            .Where(e => e.SeriesId == seriesId && e.StartTime >= from && !e.IsSeriesException)
            .Select(e => e.Id)
            .ToListAsync();
        if (ids.Count == 0) return;
        await _db.CalendarEventResources.Where(r => ids.Contains(r.CalendarEventId)).DeleteAsync();
        await _db.CalendarEvents.Where(e => ids.Contains(e.Id)).DeleteAsync();
    }

    // --- Hintergrunddienst ---

    /// <summary>
    /// Dienste, fuer die jetzt eine Anwesenheitsliste faellig ist. Der Vorlauf steht je
    /// Termin in MinutesBeforeEvent; die Filterung passiert im Speicher, weil LinqToDB
    /// Datumsarithmetik auf Spalten nicht uebersetzen kann (gleiches Muster wie
    /// ScheduledListRepository.GetDueAsync).
    /// </summary>
    public async Task<List<CalendarEvent>> GetDueDienstEventsAsync(DateTime now, DateTime horizon)
    {
        var candidates = await _db.CalendarEvents
            .Where(e => e.Type == CalendarEventType.Dienst
                        && e.AttendanceListId == null
                        && e.Status != CalendarEventStatus.Storniert
                        && e.Status != CalendarEventStatus.Abgelehnt
                        && e.StartTime <= horizon
                        && e.EndTime > now)
            .ToListAsync();

        return candidates
            .Where(e => e.StartTime.AddMinutes(-e.MinutesBeforeEvent) <= now)
            .OrderBy(e => e.StartTime)
            .ToList();
    }

    public async Task<DataConnectionTransaction> BeginTransactionAsync()
    {
        return await _db.BeginTransactionAsync();
    }
}
