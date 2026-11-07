using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;

namespace FeuerwehrListen.Services;

/// <summary>
/// Ablauflogik des Fuehrungskreises: Protokoll je Sitzung anlegen, erledigte Aufgaben als
/// Punkt der naechsten Sitzung einplanen, Sitzung starten/abschliessen, Anwesenheit, PDF.
/// Siehe PLAN-MITGLIEDERBEREICH-FUEHRUNGSKREIS.md, Teil 2.
/// </summary>
public class LeadershipService
{
    private readonly LeadershipRepository _repo;
    private readonly CalendarRepository _calendar;
    private readonly AttendanceListRepository _lists;
    private readonly AttendanceEntryRepository _entries;
    private readonly MemberRepository _members;
    private readonly EmailSenderService _email;
    private readonly ILogger<LeadershipService> _logger;

    public LeadershipService(LeadershipRepository repo, CalendarRepository calendar, AttendanceListRepository lists,
        AttendanceEntryRepository entries, MemberRepository members, EmailSenderService email, ILogger<LeadershipService> logger)
    {
        _repo = repo;
        _calendar = calendar;
        _lists = lists;
        _entries = entries;
        _members = members;
        _email = email;
        _logger = logger;
    }

    public static int CircleOf(CalendarEvent e) =>
        LeadershipCircles.IsValid(e.LeadershipCircle ?? 0) ? e.LeadershipCircle!.Value : LeadershipCircles.Small;

    /// <summary>Protokoll zum Termin holen bzw. anlegen (mit Standardpunkten und Rueckstau).</summary>
    public async Task<LeadershipProtocol> EnsureProtocolAsync(CalendarEvent e)
    {
        var existing = await _repo.GetProtocolByEventAsync(e.Id);
        if (existing != null)
        {
            // Kreis am Termin geaendert, solange noch nichts passiert ist -> mitziehen.
            if (existing.Status == LeadershipProtocolStatus.Vorbereitung && existing.Circle != CircleOf(e))
            {
                existing.Circle = CircleOf(e);
                await _repo.UpdateProtocolAsync(existing);
            }
            return existing;
        }

        var p = new LeadershipProtocol { CalendarEventId = e.Id, Circle = CircleOf(e), CreatedAt = DateTime.Now };
        await _repo.InsertProtocolAsync(p);

        var order = 10;
        await _repo.InsertItemAsync(new LeadershipItem
        {
            ProtocolId = p.Id, Circle = p.Circle, SortOrder = order, CreatedAt = DateTime.Now,
            Title = "Begrüßung, Anwesenheit, Protokoll der letzten Sitzung"
        });
        // Wartende Punkte des Kreises (erledigte Aufgaben, vertagte Punkte) uebernehmen.
        foreach (var item in await _repo.GetBacklogAsync(p.Circle))
        {
            order += 10;
            item.ProtocolId = p.Id;
            item.SortOrder = order;
            await _repo.UpdateItemAsync(item);
        }
        await _repo.InsertItemAsync(new LeadershipItem
        {
            ProtocolId = p.Id, Circle = p.Circle, SortOrder = 10000, CreatedAt = DateTime.Now, Title = "Verschiedenes"
        });
        return p;
    }

    /// <summary>Protokolle fuer alle Fuehrungskreis-Termine im Zeitraum sicherstellen
    /// (deckt auch Serientermine und nachtraeglich angelegte Termine ab).</summary>
    public async Task EnsureProtocolsInRangeAsync(DateTime from, DateTime to)
    {
        var events = (await _calendar.GetEventsInRangeAsync(from, to))
            .Where(e => e.Type == CalendarEventType.Fuehrungskreis && e.Status != CalendarEventStatus.Storniert);
        foreach (var e in events) await EnsureProtocolAsync(e);
    }

    /// <summary>Naechste Sitzung dieses Kreises, die noch vorbereitet wird.</summary>
    public async Task<LeadershipProtocol?> NextPreparingProtocolAsync(int circle)
    {
        var protocols = (await _repo.GetProtocolsAsync())
            .Where(p => p.Circle == circle && p.Status == LeadershipProtocolStatus.Vorbereitung).ToList();
        LeadershipProtocol? best = null;
        DateTime bestStart = DateTime.MaxValue;
        foreach (var p in protocols)
        {
            var e = await _calendar.GetEventAsync(p.CalendarEventId);
            if (e == null || e.Status == CalendarEventStatus.Storniert || e.EndTime < DateTime.Now) continue;
            if (e.StartTime < bestStart) { best = p; bestStart = e.StartTime; }
        }
        return best;
    }

    /// <summary>Punkt fuer die naechste Sitzung des Kreises einplanen (sonst Rueckstau).</summary>
    public async Task<LeadershipItem> ScheduleForNextMeetingAsync(int circle, string title, int? ownerMemberId, int? sourceTaskId, string notes = "")
    {
        var next = await NextPreparingProtocolAsync(circle);
        var item = new LeadershipItem
        {
            Circle = circle, Title = title, OwnerMemberId = ownerMemberId, SourceTaskId = sourceTaskId, Notes = notes,
            ProtocolId = next?.Id, CreatedAt = DateTime.Now,
            SortOrder = next == null ? 0 : await SortOrderBeforeMiscAsync(next.Id)
        };
        await _repo.InsertItemAsync(item);
        return item;
    }

    /// <summary>Naechste Position vor "Verschiedenes" (SortOrder 10000).</summary>
    public async Task<int> SortOrderBeforeMiscAsync(int protocolId)
    {
        var max = (await _repo.GetItemsAsync(protocolId)).Where(i => i.SortOrder < 10000).Select(i => i.SortOrder).DefaultIfEmpty(10).Max();
        return Math.Min(max + 10, 9990);
    }

    /// <summary>Aufgabe erledigen -> automatisch Punkt in der naechsten Sitzung ihres Kreises.</summary>
    public async Task CompleteTaskAsync(LeadershipTask task)
    {
        if (task.Status == LeadershipTaskStatus.Erledigt) return;
        task.Status = LeadershipTaskStatus.Erledigt;
        task.CompletedAt = DateTime.Now;
        await _repo.UpdateTaskAsync(task);

        // Keine Doppelung: steht die Aufgabe bereits als offener Punkt an, reicht das.
        var linked = await _repo.GetItemsBySourceTaskAsync(task.Id);
        foreach (var it in linked)
        {
            if (it.ProtocolId == null) return;
            var p = await _repo.GetProtocolAsync(it.ProtocolId.Value);
            if (p != null && p.Status != LeadershipProtocolStatus.Abgeschlossen && it.Status == LeadershipItemStatus.Offen) return;
        }
        await ScheduleForNextMeetingAsync(task.Circle, "Erledigt: " + task.Title, task.AssigneeMemberId, task.Id);
    }

    /// <summary>Sitzung beginnen: Anwesenheitsliste (versteckt) anlegen, Status "laeuft".</summary>
    public async Task StartAsync(LeadershipProtocol p)
    {
        if (p.Status != LeadershipProtocolStatus.Vorbereitung) return;
        var e = await _calendar.GetEventAsync(p.CalendarEventId);
        if (p.AttendanceListId == null)
        {
            var listId = await _lists.CreateAsync(new AttendanceList
            {
                Title = e?.Title ?? LeadershipCircles.Label(p.Circle),
                Unit = LeadershipCircles.Label(p.Circle),
                Description = string.Empty,
                CreatedAt = DateTime.Now,
                Status = ListStatus.Open,
                LeadershipProtocolId = p.Id
            });
            p.AttendanceListId = listId;
            if (e != null && e.AttendanceListId == null)
            {
                e.AttendanceListId = listId;
                await _calendar.UpdateEventAsync(e);
            }
        }
        p.Status = LeadershipProtocolStatus.Laeuft;
        p.StartedAt = DateTime.Now;
        await _repo.UpdateProtocolAsync(p);
    }

    /// <summary>Hintergrunddienst: faellige Sitzungen (Vorlauf wie bei Diensten) automatisch starten.</summary>
    public async Task StartDueMeetingsAsync(DateTime now)
    {
        await EnsureProtocolsInRangeAsync(now.AddDays(-1), now.AddDays(120));
        var events = (await _calendar.GetEventsInRangeAsync(now.AddDays(-1), now.AddDays(1)))
            .Where(e => e.Type == CalendarEventType.Fuehrungskreis && e.Status != CalendarEventStatus.Storniert
                        && e.StartTime.AddMinutes(-e.MinutesBeforeEvent) <= now && e.EndTime > now);
        foreach (var e in events)
        {
            var p = await EnsureProtocolAsync(e);
            if (p.Status == LeadershipProtocolStatus.Vorbereitung)
            {
                await StartAsync(p);
                _logger.LogInformation("Fuehrungskreis-Sitzung {Id} '{Title}' gestartet.", p.Id, e.Title);
            }
        }
    }

    // ------------------------------------------------------------ Anwesenheit

    public enum AttendanceState { Absent = 0, Present = 1, Excused = 2 }

    private static string EntryKey(Member m) => $"({m.MemberNumber})";

    /// <summary>Kandidaten = aktive Mitglieder, die diese Sitzung sehen duerfen (Stufe &gt;= Kreis).</summary>
    public async Task<List<(Member Member, AttendanceState State)>> GetAttendanceAsync(LeadershipProtocol p)
    {
        var candidates = await _members.GetLeadershipMembersAsync(p.Circle);
        var entries = p.AttendanceListId is int lid ? await _entries.GetByListIdAsync(lid) : new List<AttendanceEntry>();
        return candidates.Select(m =>
        {
            var e = entries.FirstOrDefault(x => x.NameOrId.Contains(EntryKey(m)));
            var state = e == null ? AttendanceState.Absent : e.IsExcused ? AttendanceState.Excused : AttendanceState.Present;
            return (m, state);
        }).ToList();
    }

    public async Task<bool> SetAttendanceAsync(LeadershipProtocol p, int memberId, AttendanceState state)
    {
        if (p.AttendanceListId is not int lid || p.Status != LeadershipProtocolStatus.Laeuft) return false;
        var m = await _members.GetByIdAsync(memberId);
        if (m == null || m.LeadershipCircle < p.Circle) return false;
        foreach (var e in (await _entries.GetByListIdAsync(lid)).Where(x => x.NameOrId.Contains(EntryKey(m))))
            await _entries.DeleteAsync(e.Id);
        if (state != AttendanceState.Absent)
            await _entries.CreateAsync(new AttendanceEntry
            {
                AttendanceListId = lid, NameOrId = $"{m.FirstName} {m.LastName} {EntryKey(m)}",
                EnteredAt = DateTime.Now, IsExcused = state == AttendanceState.Excused
            });
        return true;
    }

    // ------------------------------------------------------------ Abschluss

    /// <summary>Sitzung abschliessen: nicht besprochene/vertagte Punkte wandern in die naechste
    /// Sitzung, Anwesenheit wird geschlossen, Protokoll als PDF an den Kreis.</summary>
    public async Task<(bool Mailed, int Recipients)> CloseAsync(LeadershipProtocol p, string closedBy)
    {
        if (p.Status == LeadershipProtocolStatus.Abgeschlossen) return (false, 0);
        if (p.Status == LeadershipProtocolStatus.Vorbereitung) await StartAsync(p);

        p.Status = LeadershipProtocolStatus.Abgeschlossen;
        p.ClosedAt = DateTime.Now;
        p.ClosedBy = closedBy;
        await _repo.UpdateProtocolAsync(p);

        if (p.AttendanceListId is int lid && await _lists.GetByIdAsync(lid) is AttendanceList list && list.Status == ListStatus.Open)
        {
            list.Status = ListStatus.Closed;
            list.ClosedAt = DateTime.Now;
            await _lists.UpdateAsync(list);
        }

        // Offene und vertagte Punkte (ausser den Standardpunkten) in die naechste Sitzung.
        foreach (var item in await _repo.GetItemsAsync(p.Id))
        {
            if (item.Status == LeadershipItemStatus.Besprochen) continue;
            if (item.SortOrder == 10 || item.SortOrder == 10000) continue;
            item.Status = LeadershipItemStatus.Vertagt;
            await _repo.UpdateItemAsync(item);
            await ScheduleForNextMeetingAsync(p.Circle, item.Title, item.OwnerMemberId, item.SourceTaskId,
                string.IsNullOrWhiteSpace(item.Notes) ? "" : "Aus der letzten Sitzung: " + item.Notes);
        }

        var pdf = await BuildPdfAsync(p);
        var recipients = (await _members.GetLeadershipMembersAsync(p.Circle))
            .Select(m => m.Email).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct().ToList();
        if (recipients.Count == 0) return (false, 0);

        var e = await _calendar.GetEventAsync(p.CalendarEventId);
        var date = e?.StartTime.ToString("dd.MM.yyyy") ?? DateTime.Now.ToString("dd.MM.yyyy");
        var ok = await _email.SendWithAttachmentAsync(recipients,
            $"Protokoll {LeadershipCircles.Label(p.Circle)} vom {date}",
            $"Hallo,\n\nanbei das Protokoll der Sitzung \"{e?.Title}\" vom {date}.\n\nAbgeschlossen von {closedBy}.",
            pdf, $"Protokoll-{date}.pdf");
        return (ok, recipients.Count);
    }

    // ------------------------------------------------------------ PDF

    public async Task<byte[]> BuildPdfAsync(LeadershipProtocol p)
    {
        var e = await _calendar.GetEventAsync(p.CalendarEventId);
        var items = await _repo.GetItemsAsync(p.Id);
        var tasks = await _repo.GetTasksForItemsAsync(items.Select(i => i.Id).ToList());
        var attendance = await GetAttendanceAsync(p);
        var memberNames = (await _members.GetAllAsync()).ToDictionary(m => m.Id, m => $"{m.FirstName} {m.LastName}");

        var doc = new PdfDocument();
        doc.Info.Title = "Protokoll " + (e?.Title ?? "");
        var page = doc.AddPage();
        var gfx = XGraphics.FromPdfPage(page);
        var tf = new XTextFormatter(gfx);
        XFont F(double size, bool bold = false) => new("CreatoDisplay", size, bold ? XFontStyleEx.Bold : XFontStyleEx.Regular);

        double left = 50, width = page.Width.Point - 100, y = 50, bottom = page.Height.Point - 50;

        void NewPageIfNeeded(double needed)
        {
            if (y + needed <= bottom) return;
            gfx.Dispose();
            page = doc.AddPage();
            gfx = XGraphics.FromPdfPage(page);
            tf = new XTextFormatter(gfx);
            y = 50;
        }

        void Text(string text, XFont font, XBrush? brush = null, double indent = 0, double after = 4)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            // Hoehe grob schaetzen (Zeichen je Zeile), damit der Umbruch nicht abgeschnitten wird.
            var charsPerLine = Math.Max(20, (int)((width - indent) / (font.Size * 0.5)));
            var lines = text.Split('\n').Sum(l => Math.Max(1, (int)Math.Ceiling(l.Length / (double)charsPerLine)));
            var h = lines * font.Size * 1.3;
            NewPageIfNeeded(h);
            tf.DrawString(text, font, brush ?? XBrushes.Black, new XRect(left + indent, y, width - indent, h), XStringFormats.TopLeft);
            y += h + after;
        }

        Text("PROTOKOLL – " + LeadershipCircles.Label(p.Circle).ToUpperInvariant(), F(16, true), new XSolidBrush(XColor.FromArgb(200, 16, 46)), after: 2);
        gfx.DrawLine(new XPen(XColors.DarkRed, 1), left, y, left + width, y);
        y += 10;
        Text($"{e?.Title} · {e?.StartTime:dd.MM.yyyy HH:mm} Uhr{(string.IsNullOrWhiteSpace(e?.Location) ? "" : " · " + e!.Location)}", F(11));
        Text($"Abgeschlossen: {p.ClosedAt:dd.MM.yyyy HH:mm} von {p.ClosedBy}", F(10), XBrushes.Gray, after: 10);

        var present = attendance.Where(a => a.State == AttendanceState.Present).Select(a => $"{a.Member.FirstName} {a.Member.LastName}").ToList();
        var excused = attendance.Where(a => a.State == AttendanceState.Excused).Select(a => $"{a.Member.FirstName} {a.Member.LastName}").ToList();
        var absent = attendance.Where(a => a.State == AttendanceState.Absent).Select(a => $"{a.Member.FirstName} {a.Member.LastName}").ToList();
        Text("Anwesenheit", F(13, true));
        Text($"Anwesend ({present.Count}): {string.Join(", ", present)}", F(10));
        if (excused.Count > 0) Text($"Entschuldigt ({excused.Count}): {string.Join(", ", excused)}", F(10));
        if (absent.Count > 0) Text($"Fehlend ({absent.Count}): {string.Join(", ", absent)}", F(10), after: 12);

        Text("Tagesordnung", F(13, true));
        var no = 0;
        foreach (var it in items)
        {
            no++;
            var owner = it.OwnerMemberId is int oid && memberNames.TryGetValue(oid, out var on) ? $" ({on})" : "";
            var status = it.Status == LeadershipItemStatus.Vertagt ? " – vertagt" : "";
            Text($"TOP {no}: {it.Title}{owner}{status}", F(11, true), after: 2);
            Text(it.Notes, F(10), indent: 14);
            if (!string.IsNullOrWhiteSpace(it.Decision)) Text("Beschluss: " + it.Decision, F(10, true), indent: 14);
            foreach (var t in tasks.Where(t => t.SourceItemId == it.Id))
            {
                var who = t.AssigneeMemberId is int aid && memberNames.TryGetValue(aid, out var an) ? an : "offen";
                Text($"Aufgabe: {t.Title} – {who}{(t.DueDate is DateTime d ? $", fällig {d:dd.MM.yyyy}" : "")}", F(10), indent: 14);
            }
            y += 6;
        }

        gfx.Dispose();
        using var ms = new MemoryStream();
        doc.Save(ms);
        return ms.ToArray();
    }
}
