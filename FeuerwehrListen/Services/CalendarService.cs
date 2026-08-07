using System.Security.Cryptography;
using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;

namespace FeuerwehrListen.Services;

/// <summary>Ergebnis einer Buchungsanfrage - unterscheidet bewusst zwischen Konflikt,
/// erfolgreicher Buchung und "gebucht, aber Mail ging nicht raus".</summary>
public enum CalendarBookingOutcome
{
    Created = 1,
    Conflict = 2,
    ResourceNotFound = 3,
    Invalid = 4
}

public record CalendarConflictInfo(CalendarResourceKind Kind, int ResourceId, string ResourceName, string ConflictTitle, DateTime Start, DateTime End);

public record CalendarBookingResult(
    CalendarBookingOutcome Outcome,
    int? EventId,
    List<CalendarConflictInfo> Conflicts,
    bool ApprovalRequired,
    bool MailSent,
    string? Message);

/// <summary>Entscheidung ueber einen Freigabe-Link.</summary>
public enum ApprovalOutcome
{
    Ok = 1,
    UnknownToken = 2,
    AlreadyDecided = 3,
    Expired = 4
}

public class CalendarService
{
    private readonly CalendarRepository _repo;
    private readonly VehicleRepository _vehicles;
    private readonly RoomRepository _rooms;
    private readonly EmailSenderService _email;
    private readonly AppUrlService _urls;
    private readonly SettingsService _settings;
    private readonly ILogger<CalendarService> _logger;

    public CalendarService(
        CalendarRepository repo,
        VehicleRepository vehicles,
        RoomRepository rooms,
        EmailSenderService email,
        AppUrlService urls,
        SettingsService settings,
        ILogger<CalendarService> logger)
    {
        _repo = repo;
        _vehicles = vehicles;
        _rooms = rooms;
        _email = email;
        _urls = urls;
        _settings = settings;
        _logger = logger;
    }

    // ---------------------------------------------------------------- Serien

    /// <summary>
    /// Berechnet die Starttermine einer Serie im Zeitraum [from,to).
    /// Rein funktional - keine DB-Zugriffe, damit gut testbar.
    /// </summary>
    public static List<DateTime> ComputeOccurrences(CalendarEventSeries s, DateTime from, DateTime to)
    {
        var result = new List<DateTime>();
        if (to <= from) return result;

        var rangeStart = from.Date;
        if (s.SeriesStart.Date > rangeStart) rangeStart = s.SeriesStart.Date;
        var rangeEnd = s.SeriesEnd.HasValue && s.SeriesEnd.Value < to ? s.SeriesEnd.Value : to;
        if (rangeEnd <= rangeStart) return result;

        // Sicherheitsnetz gegen Endlosschleifen bei kaputten Regeln.
        var guard = 0;
        const int maxOccurrences = 2000;

        if (s.Frequency == CalendarFrequency.Monatlich)
        {
            var day = s.DayOfMonth is int d && d >= 1 && d <= 31 ? d : s.SeriesStart.Day;
            var cursor = new DateTime(rangeStart.Year, rangeStart.Month, 1);
            while (cursor < rangeEnd && guard++ < maxOccurrences)
            {
                var daysInMonth = DateTime.DaysInMonth(cursor.Year, cursor.Month);
                // Bei "31." in kurzen Monaten auf den letzten Tag ausweichen.
                var effectiveDay = Math.Min(day, daysInMonth);
                var occurrence = new DateTime(cursor.Year, cursor.Month, effectiveDay).AddMinutes(s.StartMinuteOfDay);
                if (occurrence >= rangeStart && occurrence < rangeEnd && occurrence >= s.SeriesStart)
                    result.Add(occurrence);
                cursor = cursor.AddMonths(1);
            }
            return result;
        }

        // Woechentlich / zweiwoechentlich ueber die Wochentagsmaske.
        var stepWeeks = s.Frequency == CalendarFrequency.Zweiwoechentlich ? 2 : 1;
        var mask = s.WeekdayMask;
        if (mask == 0) mask = 1 << (int)s.SeriesStart.DayOfWeek;

        // Anker ist der Wochenbeginn (Sonntag) der Serienstart-Woche - damit die
        // Zweiwochen-Taktung stabil bleibt, egal wann man in den Zeitraum schaut.
        var anchor = s.SeriesStart.Date.AddDays(-(int)s.SeriesStart.DayOfWeek);
        var weeksFromAnchor = (int)Math.Floor((rangeStart - anchor).TotalDays / 7.0);
        if (weeksFromAnchor < 0) weeksFromAnchor = 0;
        // Auf den naechsten gueltigen Takt einrasten.
        weeksFromAnchor -= weeksFromAnchor % stepWeeks;

        var weekStart = anchor.AddDays(weeksFromAnchor * 7);
        while (weekStart < rangeEnd && guard++ < maxOccurrences)
        {
            for (var dow = 0; dow < 7; dow++)
            {
                if ((mask & (1 << dow)) == 0) continue;
                var occurrence = weekStart.AddDays(dow).AddMinutes(s.StartMinuteOfDay);
                if (occurrence >= rangeStart && occurrence < rangeEnd && occurrence >= s.SeriesStart)
                    result.Add(occurrence);
            }
            weekStart = weekStart.AddDays(7 * stepWeeks);
        }

        return result.OrderBy(x => x).ToList();
    }

    /// <summary>
    /// Materialisiert fehlende Termine einer Serie bis zum Horizont.
    /// Bereits vorhandene Termine (auch abgesagte Ausnahmen) werden NICHT neu erzeugt -
    /// dadurch bleibt eine Absage dauerhaft bestehen.
    /// </summary>
    public async Task<int> MaterializeSeriesAsync(CalendarEventSeries s, DateTime from, DateTime horizon)
    {
        var wanted = ComputeOccurrences(s, from, horizon);
        if (wanted.Count == 0) return 0;

        var existing = await _repo.GetSeriesEventsAsync(s.Id, from.Date);
        // Gegen den urspruenglichen Slot abgleichen, NICHT gegen die aktuelle Startzeit:
        // ein einzeln verschobener Termin wuerde sonst als fehlend gelten und ein zweites
        // Mal angelegt. Fallback auf StartTime fuer Altdaten ohne gesetzten Slot.
        var takenSlots = existing.Select(e => e.SeriesOccurrence ?? e.StartTime).ToHashSet();

        var created = 0;
        foreach (var start in wanted)
        {
            if (takenSlots.Contains(start)) continue;
            await _repo.InsertEventAsync(new CalendarEvent
            {
                Type = s.Type,
                Title = s.Title,
                Description = s.Description,
                Location = s.Location,
                StartTime = start,
                EndTime = start.AddMinutes(s.DurationMinutes),
                UnitNumber = s.UnitNumber,
                Status = CalendarEventStatus.Bestaetigt,
                RequestedBy = s.RequestedBy,
                SeriesId = s.Id,
                SeriesOccurrence = start,
                IsSeriesException = false,
                MinutesBeforeEvent = s.MinutesBeforeEvent,
                CreatedAt = DateTime.Now
            });
            created++;
        }
        return created;
    }

    /// <summary>Alle aktiven Serien bis zum Horizont auffuellen (rollierend).</summary>
    public async Task<int> MaterializeAllSeriesAsync(int horizonMonths = 12)
    {
        var now = DateTime.Now;
        var horizon = now.AddMonths(horizonMonths);
        var total = 0;
        foreach (var s in await _repo.GetActiveSeriesAsync())
        {
            try
            {
                total += await MaterializeSeriesAsync(s, now.Date, horizon);
            }
            catch (Exception ex)
            {
                // Eine kaputte Serie darf die anderen nicht blockieren.
                _logger.LogError(ex, "Serie {SeriesId} konnte nicht materialisiert werden.", s.Id);
            }
        }
        return total;
    }

    // -------------------------------------------------------------- Buchung

    /// <summary>
    /// Legt einen Termin mit optionalen Ressourcen an. Prueft Ueberschneidungen und
    /// erzeugt bei freigabepflichtigen Ressourcen Einmal-Tokens samt Mailversand.
    /// </summary>
    public async Task<CalendarBookingResult> CreateEventAsync(
        CalendarEvent ev,
        List<(CalendarResourceKind Kind, int Id)> resources)
    {
        if (string.IsNullOrWhiteSpace(ev.Title))
            return new CalendarBookingResult(CalendarBookingOutcome.Invalid, null, new(), false, false, "Titel fehlt.");
        if (string.IsNullOrWhiteSpace(ev.RequestedBy))
            return new CalendarBookingResult(CalendarBookingOutcome.Invalid, null, new(), false, false, "Bitte Name oder Mitgliedsnummer angeben.");
        if (ev.EndTime <= ev.StartTime)
            return new CalendarBookingResult(CalendarBookingOutcome.Invalid, null, new(), false, false, "Ende muss nach dem Beginn liegen.");

        // Ressourcen aufloesen und Freigabebedarf bestimmen.
        var resolved = new List<(CalendarResourceKind Kind, int Id, string Name, bool NeedsApproval, string? Approvers)>();
        foreach (var (kind, id) in resources.Distinct())
        {
            if (kind == CalendarResourceKind.Vehicle)
            {
                var v = await _vehicles.GetByIdAsync(id);
                if (v == null || !v.IsActive || !v.IsBookable)
                    return new CalendarBookingResult(CalendarBookingOutcome.ResourceNotFound, null, new(), false, false, "Fahrzeug nicht buchbar.");
                resolved.Add((kind, id, v.Name, v.RequiresApproval, v.ApproverEmails));
            }
            else
            {
                var r = await _rooms.GetByIdAsync(id);
                if (r == null || !r.IsActive)
                    return new CalendarBookingResult(CalendarBookingOutcome.ResourceNotFound, null, new(), false, false, "Raum nicht buchbar.");
                resolved.Add((kind, id, r.Name, r.RequiresApproval, r.ApproverEmails));
            }
        }

        // Pruefen und Einfuegen in einer Transaktion, damit zwischen Pruefung und Insert
        // keine konkurrierende Buchung dazwischenkommt.
        await using var tx = await _repo.BeginTransactionAsync();

        var conflicts = new List<CalendarConflictInfo>();
        foreach (var r in resolved)
        {
            var found = await _repo.GetConflictsAsync(r.Kind, r.Id, ev.StartTime, ev.EndTime);
            foreach (var (_, conflictEvent) in found)
                conflicts.Add(new CalendarConflictInfo(r.Kind, r.Id, r.Name, conflictEvent.Title, conflictEvent.StartTime, conflictEvent.EndTime));

            // Brandsicherheitswachen liegen in einer eigenen Tabelle und belegen ihre
            // Fahrzeuge ueber die Anforderungen - separat pruefen.
            if (r.Kind == CalendarResourceKind.Vehicle)
            {
                foreach (var (name, ws, we) in await _repo.GetWatchConflictsAsync(r.Id, ev.StartTime, ev.EndTime, GetWatchDefaultHours()))
                    conflicts.Add(new CalendarConflictInfo(r.Kind, r.Id, r.Name, $"Brandsicherheitswache: {name}", ws, we));
            }
        }

        if (conflicts.Count > 0)
        {
            await tx.RollbackAsync();
            return new CalendarBookingResult(CalendarBookingOutcome.Conflict, null, conflicts, false, false, "Zeitraum bereits belegt.");
        }

        var needsApproval = resolved.Any(r => r.NeedsApproval);
        ev.Status = needsApproval ? CalendarEventStatus.Angefragt : CalendarEventStatus.Bestaetigt;
        ev.CreatedAt = DateTime.Now;

        var eventId = await _repo.InsertEventAsync(ev);

        var tokenHours = GetApprovalTokenHours();
        var pending = new List<(CalendarEventResource Row, string Name, string? Approvers)>();

        foreach (var r in resolved)
        {
            var row = new CalendarEventResource
            {
                CalendarEventId = eventId,
                ResourceKind = r.Kind,
                ResourceId = r.Id,
                Status = r.NeedsApproval ? CalendarResourceStatus.Angefragt : CalendarResourceStatus.NichtErforderlich
            };
            if (r.NeedsApproval)
            {
                row.ApprovalToken = GenerateToken();
                row.TokenExpiresAt = DateTime.Now.AddHours(tokenHours);
            }
            row.Id = await _repo.InsertResourceAsync(row);
            if (r.NeedsApproval) pending.Add((row, r.Name, r.Approvers));
        }

        await tx.CommitAsync();

        // Mailversand bewusst NACH dem Commit - ein haengender SMTP-Server darf die
        // Buchung nicht zurueckrollen.
        var mailSent = true;
        foreach (var (row, name, approvers) in pending)
        {
            var ok = await SendApprovalMailAsync(ev, row, name, approvers);
            if (!ok) mailSent = false;
        }

        string? message = null;
        if (pending.Count > 0 && !mailSent)
            message = "Buchung angelegt, aber der Freigabe-Link konnte nicht versendet werden (SMTP pruefen).";

        return new CalendarBookingResult(CalendarBookingOutcome.Created, eventId, new(), needsApproval, mailSent, message);
    }

    private int GetApprovalTokenHours()
    {
        var raw = _settings.GetSetting(SettingKeys.CalendarApprovalTokenHours);
        return int.TryParse(raw, out var h) && h > 0 ? h : 168; // 7 Tage
    }

    /// <summary>Angenommene Wachendauer, wenn kein Ende hinterlegt ist (Altdaten).</summary>
    public int GetWatchDefaultHours()
    {
        var raw = _settings.GetSetting(SettingKeys.CalendarFireSafetyWatchDefaultHours);
        return int.TryParse(raw, out var h) && h > 0 ? h : 4;
    }

    /// <summary>
    /// Gegenrichtung: Fahrzeuge, die im Zeitraum der geplanten Wache bereits im Kalender
    /// gebucht sind. Leere Liste = Wache kann angelegt werden.
    /// </summary>
    public async Task<List<CalendarConflictInfo>> GetWatchVehicleConflictsAsync(
        IEnumerable<int> vehicleIds, DateTime start, DateTime end)
    {
        var result = new List<CalendarConflictInfo>();
        foreach (var id in vehicleIds.Distinct())
        {
            var vehicle = await _vehicles.GetByIdAsync(id);
            if (vehicle == null) continue;
            foreach (var (title, s, e) in await _repo.GetVehicleBookingsAsync(id, start, end))
                result.Add(new CalendarConflictInfo(CalendarResourceKind.Vehicle, id, vehicle.Name, title, s, e));
        }
        return result;
    }

    /// <summary>Kryptografisch sicheres Token - bewusst nicht Guid.NewGuid().</summary>
    private static string GenerateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    private async Task<bool> SendApprovalMailAsync(CalendarEvent ev, CalendarEventResource row, string resourceName, string? approverEmails)
    {
        if (string.IsNullOrWhiteSpace(approverEmails))
        {
            _logger.LogWarning("Freigabe noetig, aber keine Zustaendigen fuer {Kind} {Id} hinterlegt.", row.ResourceKind, row.ResourceId);
            return false;
        }

        var link = _urls.BuildApproveUrl($"approve/{row.ApprovalToken}");
        if (!_urls.HasAbsoluteBase())
            _logger.LogWarning("App.BaseUrl ist nicht gesetzt - der Freigabe-Link in der Mail ist relativ und damit unbrauchbar.");

        var art = row.ResourceKind == CalendarResourceKind.Vehicle ? "Fahrzeug" : "Raum";
        var body =
            $"Es liegt eine Buchungsanfrage vor.\r\n\r\n" +
            $"{art}: {resourceName}\r\n" +
            $"Anlass: {ev.Title}\r\n" +
            $"Zeitraum: {ev.StartTime:dd.MM.yyyy HH:mm} - {ev.EndTime:dd.MM.yyyy HH:mm}\r\n" +
            $"Angefragt von: {ev.RequestedBy}\r\n" +
            (string.IsNullOrWhiteSpace(ev.Description) ? "" : $"Hinweis: {ev.Description}\r\n") +
            $"\r\nZum Freigeben oder Ablehnen diesen Link oeffnen:\r\n" +
            $"{link}\r\n\r\n" +
            $"Der Link ist bis {row.TokenExpiresAt:dd.MM.yyyy HH:mm} gueltig und kann nur einmal verwendet werden.\r\n";

        return await _email.SendAsync(new[] { approverEmails }, $"Buchungsanfrage: {resourceName} ({ev.StartTime:dd.MM.yyyy})", body);
    }

    // -------------------------------------------------------------- Freigabe

    /// <summary>Liest die Buchung zu einem Token, ohne etwas zu veraendern.</summary>
    public async Task<(ApprovalOutcome Outcome, CalendarEventResource? Resource, CalendarEvent? Event, string ResourceName)> LoadApprovalAsync(string token)
    {
        var row = await _repo.GetResourceByTokenAsync(token);
        if (row == null) return (ApprovalOutcome.UnknownToken, null, null, "");
        if (row.TokenUsedAt != null) return (ApprovalOutcome.AlreadyDecided, row, await _repo.GetEventAsync(row.CalendarEventId), await ResourceNameAsync(row));
        if (row.TokenExpiresAt is DateTime exp && exp < DateTime.Now)
            return (ApprovalOutcome.Expired, row, await _repo.GetEventAsync(row.CalendarEventId), await ResourceNameAsync(row));

        var ev = await _repo.GetEventAsync(row.CalendarEventId);
        if (ev == null) return (ApprovalOutcome.UnknownToken, null, null, "");
        return (ApprovalOutcome.Ok, row, ev, await ResourceNameAsync(row));
    }

    /// <summary>
    /// Entscheidung eintragen. Nur ueber POST aufrufen - ein GET wuerde von Mail-Scannern
    /// und Browser-Prefetch automatisch ausgeloest und damit ungewollt freigeben.
    /// </summary>
    public async Task<ApprovalOutcome> DecideAsync(string token, bool approve, string decidedBy, string? comment)
    {
        var (outcome, row, ev, _) = await LoadApprovalAsync(token);
        if (outcome != ApprovalOutcome.Ok || row == null || ev == null) return outcome;

        row.Status = approve ? CalendarResourceStatus.Freigegeben : CalendarResourceStatus.Abgelehnt;
        row.TokenUsedAt = DateTime.Now;
        row.ApprovedAt = DateTime.Now;
        row.ApprovedBy = string.IsNullOrWhiteSpace(decidedBy) ? "unbekannt" : decidedBy.Trim();
        row.DecisionComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        await _repo.UpdateResourceAsync(row);

        // Gesamtstatus des Termins nachziehen.
        var all = await _repo.GetResourcesForEventAsync(ev.Id);
        if (all.Any(r => r.Status == CalendarResourceStatus.Abgelehnt))
            ev.Status = CalendarEventStatus.Abgelehnt;
        else if (all.All(r => r.Status is CalendarResourceStatus.Freigegeben or CalendarResourceStatus.NichtErforderlich))
            ev.Status = CalendarEventStatus.Bestaetigt;
        else
            ev.Status = CalendarEventStatus.Angefragt;
        await _repo.UpdateEventAsync(ev);

        await NotifyRequesterAsync(ev, row, approve);
        return ApprovalOutcome.Ok;
    }

    private async Task NotifyRequesterAsync(CalendarEvent ev, CalendarEventResource row, bool approved)
    {
        if (string.IsNullOrWhiteSpace(ev.RequestedByEmail)) return;
        try
        {
            var name = await ResourceNameAsync(row);
            var wort = approved ? "freigegeben" : "abgelehnt";
            var body =
                $"Deine Buchungsanfrage wurde {wort}.\r\n\r\n" +
                $"Anlass: {ev.Title}\r\n" +
                $"Ressource: {name}\r\n" +
                $"Zeitraum: {ev.StartTime:dd.MM.yyyy HH:mm} - {ev.EndTime:dd.MM.yyyy HH:mm}\r\n" +
                $"Entschieden von: {row.ApprovedBy}\r\n" +
                (string.IsNullOrWhiteSpace(row.DecisionComment) ? "" : $"Kommentar: {row.DecisionComment}\r\n");
            await _email.SendAsync(new[] { ev.RequestedByEmail! }, $"Buchung {wort}: {ev.Title}", body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Rueckmeldung an den Antragsteller konnte nicht versendet werden.");
        }
    }

    public async Task<string> ResourceNameAsync(CalendarEventResource row)
    {
        if (row.ResourceKind == CalendarResourceKind.Vehicle)
            return (await _vehicles.GetByIdAsync(row.ResourceId))?.Name ?? $"Fahrzeug #{row.ResourceId}";
        return (await _rooms.GetByIdAsync(row.ResourceId))?.Name ?? $"Raum #{row.ResourceId}";
    }
}
