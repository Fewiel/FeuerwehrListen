using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;
using FeuerwehrListen.Services;

namespace FeuerwehrListen.Endpoints;

/// <summary>
/// Fuehrungskreis: Aufgaben, Protokolle (Vorbereitung, Sitzung, Abschluss), Anwesenheit.
/// Grundschranke ist das Modul "leadership" (Middleware); hier zusaetzlich je Eintrag die
/// Kreis-Stufe: kleiner Kreis (2) sieht klein + gross, grosser Kreis (1) nur gross.
/// </summary>
public static class LeadershipEndpoints
{
    public static void MapLeadership(this WebApplication app)
    {
        var g = app.MapGroup("/client-api/leadership").RequireAuthorization().DisableAntiforgery();

        // Wer darf Aufgaben/Punkte uebernehmen? Mitglieder, die den Kreis sehen.
        g.MapGet("/members", async (int? circle, bool? all, MemberRepository members) =>
            Results.Json((all == true
                    ? (await members.GetActiveAsync()).OrderBy(m => m.LastName).ThenBy(m => m.FirstName).ToList()
                    : await members.GetLeadershipMembersAsync(LeadershipCircles.IsValid(circle ?? 0) ? circle!.Value : LeadershipCircles.Large))
                .Select(m => new { id = m.Id, name = $"{m.FirstName} {m.LastName}", circle = m.LeadershipCircle })));

        // ------------------------------------------------------------ Aufgaben
        g.MapGet("/tasks", async (HttpContext http, string? status, UserAccessService ua, LeadershipRepository repo, MemberRepository members) =>
        {
            var viewer = ua.Get(http)!;
            LeadershipTaskStatus? st = status switch { "done" => LeadershipTaskStatus.Erledigt, "all" => null, _ => LeadershipTaskStatus.Offen };
            var tasks = (await repo.GetTasksAsync(st)).Where(t => viewer.CanSeeCircle(t.Circle)).ToList();
            if (st == LeadershipTaskStatus.Erledigt) tasks = tasks.OrderByDescending(t => t.CompletedAt).Take(100).ToList();

            var names = await MemberNamesAsync(members);
            var items = await repo.GetItemsByIdsAsync(tasks.Where(t => t.SourceItemId != null).Select(t => t.SourceItemId!.Value).Distinct().ToList());
            var followUps = new Dictionary<int, LeadershipItem>();
            foreach (var t in tasks.Where(t => t.Status == LeadershipTaskStatus.Erledigt))
                if ((await repo.GetItemsBySourceTaskAsync(t.Id)).OrderByDescending(i => i.Id).FirstOrDefault() is LeadershipItem fu) followUps[t.Id] = fu;
            var protocolDates = await ProtocolDatesAsync(repo, http.RequestServices.GetRequiredService<CalendarRepository>());

            return Results.Json(tasks.Select(t =>
            {
                var src = t.SourceItemId is int sid ? items.FirstOrDefault(i => i.Id == sid) : null;
                followUps.TryGetValue(t.Id, out var fu);
                return new
                {
                    id = t.Id,
                    title = t.Title,
                    description = t.Description,
                    circle = t.Circle,
                    assigneeId = t.AssigneeMemberId,
                    assignee = t.AssigneeMemberId is int a && names.TryGetValue(a, out var n) ? n : null,
                    mine = viewer.MemberId != null && t.AssigneeMemberId == viewer.MemberId,
                    due = t.DueDate,
                    overdue = t.Status == LeadershipTaskStatus.Offen && t.DueDate is DateTime d && d.Date < DateTime.Today,
                    done = t.Status == LeadershipTaskStatus.Erledigt,
                    status = t.Status.ToString(),
                    notes = t.Notes,
                    completedAt = t.CompletedAt,
                    origin = src == null ? "manuell angelegt"
                        : $"{src.Title}{(src.ProtocolId is int pid && protocolDates.TryGetValue(pid, out var pd) ? $" · Sitzung {pd:dd.MM.}" : "")}",
                    followUp = fu == null ? null
                        : fu.ProtocolId is int fpid && protocolDates.TryGetValue(fpid, out var fd) ? $"Sitzung {fd:dd.MM.}" : "nächste Sitzung"
                };
            }));
        });

        g.MapPost("/tasks", async (HttpContext http, UserAccessService ua, LeadershipRepository repo, PushService push, TaskRequest r) =>
        {
            var viewer = ua.Get(http)!;
            if (string.IsNullOrWhiteSpace(r.Title)) return Results.BadRequest();
            var circle = await ResolveCircleAsync(repo, r.Circle, r.SourceItemId);
            if (!viewer.CanSeeCircle(circle)) return Results.Forbid();
            var t = new LeadershipTask
            {
                Circle = circle,
                Title = r.Title.Trim(),
                Description = r.Description?.Trim() ?? "",
                AssigneeMemberId = r.AssigneeMemberId,
                DueDate = r.DueDate?.Date,
                SourceItemId = r.SourceItemId,
                CreatedBy = DisplayName(http),
                CreatedAt = DateTime.Now
            };
            await repo.InsertTaskAsync(t);
            if (t.AssigneeMemberId is int a && a != ua.Get(http)!.MemberId)
                await push.SendToMemberAsync(a, "Neue Aufgabe für dich", t.Title + (t.DueDate is DateTime d ? $" (fällig {d:dd.MM.})" : ""), "/mein/aufgaben");
            return Results.Json(new { id = t.Id });
        });

        g.MapPut("/tasks/{id:int}", async (int id, HttpContext http, UserAccessService ua, LeadershipRepository repo, PushService push, TaskRequest r) =>
        {
            var viewer = ua.Get(http)!;
            var t = await repo.GetTaskAsync(id);
            if (t == null || !viewer.CanSeeCircle(t.Circle)) return Results.NotFound();
            if (!string.IsNullOrWhiteSpace(r.Title)) t.Title = r.Title.Trim();
            t.Description = r.Description?.Trim() ?? "";
            var newlyAssigned = r.AssigneeMemberId is int na && na != t.AssigneeMemberId && na != viewer.MemberId ? na : (int?)null;
            t.AssigneeMemberId = r.AssigneeMemberId;
            if (newlyAssigned is int nm) await push.SendToMemberAsync(nm, "Neue Aufgabe für dich", t.Title, "/mein/aufgaben");
            t.DueDate = r.DueDate?.Date;
            if (r.Circle is int c && LeadershipCircles.IsValid(c) && viewer.CanSeeCircle(c)) t.Circle = c;
            await repo.UpdateTaskAsync(t);
            return Results.Ok();
        });

        // Erledigt -> wird automatisch Punkt der naechsten Sitzung des Kreises.
        g.MapPost("/tasks/{id:int}/done", async (int id, HttpContext http, UserAccessService ua, LeadershipRepository repo, LeadershipService svc) =>
        {
            var t = await repo.GetTaskAsync(id);
            if (t == null || !ua.Get(http)!.CanSeeCircle(t.Circle)) return Results.NotFound();
            await svc.CompleteTaskAsync(t);
            return Results.Ok();
        });

        // Aus einer laufenden Aufgabe einen Tagespunkt fuer die naechste Sitzung des Kreises machen.
        g.MapPost("/tasks/{id:int}/to-agenda", async (int id, HttpContext http, UserAccessService ua, LeadershipRepository repo, LeadershipService svc, NoteRequest r) =>
        {
            var t = await repo.GetTaskAsync(id);
            if (t == null || !ua.Get(http)!.CanSeeCircle(t.Circle)) return Results.NotFound();
            var item = await svc.ScheduleForNextMeetingAsync(t.Circle, t.Title, t.AssigneeMemberId, t.Id, r.Note?.Trim() ?? t.Notes);
            return Results.Json(new { ok = true, scheduled = item.ProtocolId != null });
        });

        g.MapPost("/tasks/{id:int}/reopen", async (int id, HttpContext http, UserAccessService ua, LeadershipRepository repo) =>
        {
            var t = await repo.GetTaskAsync(id);
            if (t == null || !ua.Get(http)!.CanSeeCircle(t.Circle)) return Results.NotFound();
            t.Status = LeadershipTaskStatus.Offen;
            t.CompletedAt = null;
            await repo.UpdateTaskAsync(t);
            return Results.Ok();
        });

        g.MapDelete("/tasks/{id:int}", async (int id, HttpContext http, UserAccessService ua, LeadershipRepository repo) =>
        {
            var t = await repo.GetTaskAsync(id);
            if (t == null || !ua.Get(http)!.CanSeeCircle(t.Circle)) return Results.NotFound();
            await repo.DeleteTaskAsync(id);
            return Results.Ok();
        });

        // ------------------------------------------------------------ Protokolle
        g.MapGet("/protocols", async (HttpContext http, UserAccessService ua, LeadershipRepository repo, LeadershipService svc, CalendarRepository calendar) =>
        {
            var viewer = ua.Get(http)!;
            await svc.EnsureProtocolsInRangeAsync(DateTime.Now.AddDays(-1), DateTime.Now.AddDays(120));
            var result = new List<(DateTime Start, object Dto)>();
            foreach (var p in (await repo.GetProtocolsAsync()).Where(p => viewer.CanSeeCircle(p.Circle)))
            {
                var e = await calendar.GetEventAsync(p.CalendarEventId);
                if (e == null || (e.Status == CalendarEventStatus.Storniert && p.Status == LeadershipProtocolStatus.Vorbereitung)) continue;
                result.Add((e.StartTime, new
                {
                    id = p.Id, title = e.Title, start = e.StartTime, location = e.Location,
                    circle = p.Circle, circleLabel = LeadershipCircles.Label(p.Circle),
                    status = p.Status.ToString(), items = (await repo.GetItemsAsync(p.Id)).Count
                }));
            }
            var now = DateTime.Now.AddHours(-6);
            return Results.Json(new
            {
                upcoming = result.Where(x => x.Start >= now).OrderBy(x => x.Start).Select(x => x.Dto),
                past = result.Where(x => x.Start < now).OrderByDescending(x => x.Start).Take(50).Select(x => x.Dto)
            });
        });

        g.MapGet("/protocols/by-event/{eventId:int}", async (int eventId, HttpContext http, UserAccessService ua, CalendarRepository calendar, LeadershipService svc) =>
        {
            var e = await calendar.GetEventAsync(eventId);
            if (e == null || e.Type != CalendarEventType.Fuehrungskreis || !ua.Get(http)!.CanSeeCircle(LeadershipService.CircleOf(e))) return Results.NotFound();
            var p = await svc.EnsureProtocolAsync(e);
            return Results.Json(new { id = p.Id });
        });

        g.MapGet("/protocols/{id:int}", async (int id, HttpContext http, UserAccessService ua, LeadershipRepository repo,
            LeadershipService svc, CalendarRepository calendar, MemberRepository members) =>
        {
            var viewer = ua.Get(http)!;
            var p = await repo.GetProtocolAsync(id);
            if (p == null || !viewer.CanSeeCircle(p.Circle)) return Results.NotFound();
            var e = await calendar.GetEventAsync(p.CalendarEventId);
            var items = await repo.GetItemsAsync(p.Id);
            var tasks = await repo.GetTasksForItemsAsync(items.Select(i => i.Id).ToList());
            var names = await MemberNamesAsync(members);
            string? Name(int? mid) => mid is int m && names.TryGetValue(m, out var n) ? n : null;

            // Fuer "Aus Aufgaben uebernehmen": offene Aufgaben, die dieser Kreis sehen darf und
            // die hier noch nicht als Punkt stehen.
            var linkedTaskIds = items.Where(i => i.SourceTaskId != null).Select(i => i.SourceTaskId!.Value).ToHashSet();
            var openTasks = (await repo.GetTasksAsync(LeadershipTaskStatus.Offen))
                .Where(t => t.Circle <= p.Circle && viewer.CanSeeCircle(t.Circle) && !linkedTaskIds.Contains(t.Id));

            var attendance = p.Status == LeadershipProtocolStatus.Vorbereitung ? null
                : (await svc.GetAttendanceAsync(p)).Select(a => new
                {
                    memberId = a.Member.Id, name = $"{a.Member.FirstName} {a.Member.LastName}", state = (int)a.State
                });

            var canManage = viewer.IsAdmin || (viewer.MemberId != null && viewer.MemberId == p.KeeperMemberId);
            // Themen/Bereiche, die in dieser Sitzung nutzbar sind: Kreis <= Sitzungskreis (klein sieht auch gross).
            var allTopics = await repo.GetTopicsAsync();
            var topicTitle = allTopics.ToDictionary(t => t.Id, t => t.Title);
            return Results.Json(new
            {
                id = p.Id,
                eventId = p.CalendarEventId,
                keeperId = p.KeeperMemberId,
                keeper = Name(p.KeeperMemberId),
                canManage = canManage && p.Status != LeadershipProtocolStatus.Abgeschlossen,
                title = e?.Title,
                start = e?.StartTime,
                location = e?.Location,
                minutesBefore = e?.MinutesBeforeEvent,
                circle = p.Circle,
                circleLabel = LeadershipCircles.Label(p.Circle),
                status = p.Status.ToString(),
                startedAt = p.StartedAt,
                closedAt = p.ClosedAt,
                closedBy = p.ClosedBy,
                canEdit = p.Status != LeadershipProtocolStatus.Abgeschlossen || viewer.IsAdmin,
                items = items.Select(i => new
                {
                    id = i.Id, title = i.Title, ownerId = i.OwnerMemberId, owner = Name(i.OwnerMemberId),
                    notes = i.Notes, decision = i.Decision, status = i.Status.ToString(), section = i.Section,
                    topicId = i.TopicId, topic = i.TopicId is int ti ? topicTitle.GetValueOrDefault(ti) : null,
                    areaId = i.AreaId, area = i.AreaId is int ai ? topicTitle.GetValueOrDefault(ai) : null,
                    fromTask = i.SourceTaskId != null,
                    fixedItem = i.SortOrder == 10 || i.SortOrder == 10000,
                    tasks = tasks.Where(t => t.SourceItemId == i.Id).Select(t => new
                    {
                        id = t.Id, title = t.Title, assignee = Name(t.AssigneeMemberId), due = t.DueDate,
                        done = t.Status == LeadershipTaskStatus.Erledigt
                    })
                }),
                topics = allTopics.Where(t => !t.IsArchived && t.Circle <= p.Circle && viewer.CanSeeCircle(t.Circle))
                    .Select(t => new { id = t.Id, title = t.Title, kind = t.Kind, circle = t.Circle }),
                openTasks = openTasks.Select(t => new { id = t.Id, title = t.Title, assignee = Name(t.AssigneeMemberId), due = t.DueDate,
                    overdue = t.DueDate is DateTime d && d.Date < DateTime.Today }),
                attendance
            });
        });

        // Admin: Protokoll endgueltig loeschen - samt Kalendertermin (sonst wuerde es neu angelegt).
        g.MapDelete("/protocols/{id:int}", async (int id, HttpContext http, UserAccessService ua, LeadershipRepository repo, CalendarRepository calendar) =>
        {
            if (!http.User.IsInRole("Admin")) return Results.Forbid();
            var p = await repo.GetProtocolAsync(id);
            if (p == null) return Results.NotFound();
            await repo.DeleteProtocolAsync(p);
            await calendar.DeleteEventAsync(p.CalendarEventId);
            return Results.Ok();
        });

        // ------------------------------------------------ Themenbereiche / Bereiche
        g.MapGet("/topics", async (HttpContext http, UserAccessService ua, LeadershipRepository repo, CalendarRepository calendar) =>
        {
            var viewer = ua.Get(http)!;
            var protocolDates = await ProtocolDatesAsync(repo, calendar);
            var result = new List<object>();
            foreach (var t in (await repo.GetTopicsAsync()).Where(t => viewer.CanSeeCircle(t.Circle)))
            {
                var entries = await repo.GetItemsForTopicAsync(t.Id);
                result.Add(new
                {
                    id = t.Id, title = t.Title, kind = t.Kind, circle = t.Circle, circleLabel = LeadershipCircles.Label(t.Circle), isArchived = t.IsArchived,
                    entries = entries.Where(e => e.Status != LeadershipItemStatus.Abgelehnt).Select(e => new
                    {
                        id = e.Id, title = e.Title, notes = e.Notes, createdAt = e.CreatedAt,
                        topic = e.TopicId is int ti && ti != t.Id ? (int?)ti : null,
                        meeting = e.ProtocolId is int pid && protocolDates.TryGetValue(pid, out var d) ? d : (DateTime?)null
                    })
                });
            }
            return Results.Json(result);
        });

        g.MapPost("/topics", async (HttpContext http, UserAccessService ua, LeadershipRepository repo, TopicRequest r) =>
        {
            var circle = LeadershipCircles.IsValid(r.Circle ?? 0) ? r.Circle!.Value : LeadershipCircles.Large;
            if (string.IsNullOrWhiteSpace(r.Title) || !ua.Get(http)!.CanSeeCircle(circle)) return Results.BadRequest();
            var t = new LeadershipTopic { Title = r.Title.Trim(), Circle = circle, Kind = r.Kind == LeadershipTopic.KindArea ? LeadershipTopic.KindArea : LeadershipTopic.KindTopic, CreatedAt = DateTime.Now };
            await repo.InsertTopicAsync(t);
            return Results.Json(new { id = t.Id });
        });

        g.MapPut("/topics/{id:int}", async (int id, HttpContext http, UserAccessService ua, LeadershipRepository repo, TopicRequest r) =>
        {
            var t = await repo.GetTopicAsync(id);
            if (t == null || !ua.Get(http)!.CanSeeCircle(t.Circle)) return Results.NotFound();
            if (!string.IsNullOrWhiteSpace(r.Title)) t.Title = r.Title.Trim();
            if (r.IsArchived is bool a) t.IsArchived = a;
            await repo.UpdateTopicAsync(t);
            return Results.Ok();
        });

        // In ein Thema/einen Bereich schreiben: landet als Punkt in der naechsten Sitzung des Kreises.
        g.MapPost("/topics/{id:int}/entries", async (int id, HttpContext http, UserAccessService ua, LeadershipRepository repo, LeadershipService svc, TopicEntryRequest r) =>
        {
            var viewer = ua.Get(http)!;
            var t = await repo.GetTopicAsync(id);
            if (t == null || !viewer.CanSeeCircle(t.Circle) || string.IsNullOrWhiteSpace(r.Title)) return Results.BadRequest();
            var isArea = t.Kind == LeadershipTopic.KindArea;
            var item = await svc.ScheduleForNextMeetingAsync(t.Circle, r.Title.Trim(), viewer.MemberId, null, r.Notes?.Trim() ?? "",
                isArea ? LeadershipItem.SectionAreas : LeadershipItem.SectionInfo, isArea ? r.TopicId : t.Id, isArea ? t.Id : null);
            return Results.Json(new { ok = true, scheduled = item.ProtocolId != null });
        });

        // Kleiner -> grosser Kreis: ausgewaehlte Info in die naechste Sitzung des grossen Kreises mitnehmen.
        g.MapPost("/items/{id:int}/forward", async (int id, HttpContext http, UserAccessService ua, LeadershipRepository repo, LeadershipService svc) =>
        {
            var viewer = ua.Get(http)!;
            var it = await repo.GetItemAsync(id);
            if (it == null || it.Circle != LeadershipCircles.Small || !viewer.CanSeeCircle(LeadershipCircles.Small)) return Results.NotFound();
            // Themen des kleinen Kreises sieht der grosse nicht -> Thema in den Titel, Verknuepfung nur bei grossen Themen.
            var topic = it.TopicId is int ti ? await repo.GetTopicAsync(ti) : null;
            var keepTopic = topic != null && topic.Circle == LeadershipCircles.Large;
            var title = topic != null && !keepTopic ? $"{topic.Title}: {it.Title}" : it.Title;
            var section = it.Section == LeadershipItem.SectionAgenda ? LeadershipItem.SectionInfo : it.Section;
            var item = await svc.ScheduleForNextMeetingAsync(LeadershipCircles.Large, title, it.OwnerMemberId, null,
                (it.Notes ?? "") + "\n(aus dem kleinen Führungskreis)", section, keepTopic ? topic!.Id : null, null);
            return Results.Json(new { ok = true, scheduled = item.ProtocolId != null });
        });

        g.MapPut("/protocols/{id:int}/keeper", async (int id, HttpContext http, UserAccessService ua, LeadershipRepository repo, KeeperRequest r) =>
        {
            var p = await EditableProtocolAsync(id, http, ua, repo);
            if (p == null) return Results.NotFound();
            var viewer = ua.Get(http)!;
            // Erstmals setzen darf jeder im Kreis, aendern nur Protokollfuehrung/Admin.
            if (p.KeeperMemberId != null && !viewer.IsAdmin && viewer.MemberId != p.KeeperMemberId) return Results.Forbid();
            p.KeeperMemberId = r.MemberId;
            await repo.UpdateProtocolAsync(p);
            return Results.Ok();
        });

        g.MapPost("/protocols/{id:int}/send-agenda", async (int id, HttpContext http, UserAccessService ua, LeadershipRepository repo, LeadershipService svc) =>
        {
            var p = await EditableProtocolAsync(id, http, ua, repo);
            if (p == null) return Results.NotFound();
            var (mailed, recipients) = await svc.SendAgendaAsync(p);
            return Results.Json(new { mailed, recipients });
        });

        g.MapPost("/protocols/{id:int}/items", async (int id, HttpContext http, UserAccessService ua, LeadershipRepository repo, LeadershipService svc, ItemRequest r) =>
        {
            var p = await EditableProtocolAsync(id, http, ua, repo);
            if (p == null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(r.Title)) return Results.BadRequest();
            var section = r.Section is LeadershipItem.SectionInfo or LeadershipItem.SectionAreas ? r.Section.Value : LeadershipItem.SectionAgenda;
            // Infos/Bereiche: ohne Angabe ist der Einreichende verantwortlich.
            var owner = r.OwnerMemberId ?? (section != LeadershipItem.SectionAgenda ? ua.Get(http)!.MemberId : null);
            var item = new LeadershipItem
            {
                ProtocolId = p.Id, Circle = p.Circle, Title = r.Title.Trim(), OwnerMemberId = owner, Section = section,
                TopicId = section == LeadershipItem.SectionAgenda ? null : r.TopicId,
                AreaId = section == LeadershipItem.SectionAreas ? r.AreaId : null,
                Notes = r.Notes?.Trim() ?? "",
                SortOrder = section == LeadershipItem.SectionAgenda ? await svc.SortOrderBeforeMiscAsync(p.Id) : 0,
                CreatedAt = DateTime.Now
            };
            await repo.InsertItemAsync(item);
            return Results.Json(new { id = item.Id });
        });

        g.MapPost("/protocols/{id:int}/items/from-task/{taskId:int}", async (int id, int taskId, HttpContext http, UserAccessService ua,
            LeadershipRepository repo, LeadershipService svc) =>
        {
            var p = await EditableProtocolAsync(id, http, ua, repo);
            var t = await repo.GetTaskAsync(taskId);
            if (p == null || t == null || !ua.Get(http)!.CanSeeCircle(t.Circle)) return Results.NotFound();
            var item = new LeadershipItem
            {
                ProtocolId = p.Id, Circle = p.Circle, Title = t.Title, OwnerMemberId = t.AssigneeMemberId, SourceTaskId = t.Id,
                SortOrder = await svc.SortOrderBeforeMiscAsync(p.Id), CreatedAt = DateTime.Now
            };
            await repo.InsertItemAsync(item);
            return Results.Json(new { id = item.Id });
        });

        g.MapPut("/items/{id:int}", async (int id, HttpContext http, UserAccessService ua, LeadershipRepository repo, ItemRequest r) =>
        {
            var item = await repo.GetItemAsync(id);
            if (item?.ProtocolId is not int pid || await EditableProtocolAsync(pid, http, ua, repo) == null) return Results.NotFound();
            if (!string.IsNullOrWhiteSpace(r.Title)) item.Title = r.Title.Trim();
            item.OwnerMemberId = r.OwnerMemberId;
            if (r.Notes != null) item.Notes = r.Notes;
            if (r.Decision != null) item.Decision = r.Decision.Trim();
            if (Enum.TryParse<LeadershipItemStatus>(r.Status, true, out var st))
            {
                // Ablehnen (und wieder zulassen) nur durch die Protokollfuehrung bzw. Admin.
                var involvesReject = st == LeadershipItemStatus.Abgelehnt || item.Status == LeadershipItemStatus.Abgelehnt;
                if (involvesReject && st != item.Status && !await IsManagerAsync(pid, http, ua, repo)) return Results.Forbid();
                item.Status = st;
            }
            await repo.UpdateItemAsync(item);
            return Results.Ok();
        });

        g.MapDelete("/items/{id:int}", async (int id, HttpContext http, UserAccessService ua, LeadershipRepository repo) =>
        {
            var item = await repo.GetItemAsync(id);
            if (item?.ProtocolId is not int pid || await EditableProtocolAsync(pid, http, ua, repo) is not LeadershipProtocol p) return Results.NotFound();
            if (p.Status != LeadershipProtocolStatus.Vorbereitung) return Results.BadRequest();
            if (!await IsManagerAsync(p.Id, http, ua, repo)) return Results.Forbid();
            await repo.DeleteItemAsync(id);
            return Results.Ok();
        });

        g.MapPost("/protocols/{id:int}/reorder", async (int id, HttpContext http, UserAccessService ua, LeadershipRepository repo, ReorderRequest r) =>
        {
            var p = await EditableProtocolAsync(id, http, ua, repo);
            if (p == null) return Results.NotFound();
            if (!await IsManagerAsync(p.Id, http, ua, repo)) return Results.Forbid();
            var items = (await repo.GetItemsAsync(p.Id)).ToDictionary(i => i.Id);
            var order = 20;
            foreach (var itemId in r.Ids ?? new())
            {
                if (!items.TryGetValue(itemId, out var it) || it.SortOrder == 10 || it.SortOrder == 10000) continue;
                it.SortOrder = Math.Min(order, 9990);
                order += 10;
                await repo.UpdateItemAsync(it);
            }
            return Results.Ok();
        });

        g.MapPost("/protocols/{id:int}/start", async (int id, HttpContext http, UserAccessService ua, LeadershipRepository repo, LeadershipService svc) =>
        {
            var p = await EditableProtocolAsync(id, http, ua, repo);
            if (p == null) return Results.NotFound();
            await svc.StartAsync(p);
            return Results.Ok();
        });

        g.MapPost("/protocols/{id:int}/attendance", async (int id, HttpContext http, UserAccessService ua, LeadershipRepository repo,
            LeadershipService svc, AttendanceRequest r) =>
        {
            var p = await EditableProtocolAsync(id, http, ua, repo);
            if (p == null) return Results.NotFound();
            var state = Enum.IsDefined(typeof(LeadershipService.AttendanceState), r.State) ? (LeadershipService.AttendanceState)r.State : LeadershipService.AttendanceState.Absent;
            return await svc.SetAttendanceAsync(p, r.MemberId, state) ? Results.Ok() : Results.BadRequest();
        });

        g.MapPost("/protocols/{id:int}/close", async (int id, HttpContext http, UserAccessService ua, LeadershipRepository repo, LeadershipService svc) =>
        {
            var p = await repo.GetProtocolAsync(id);
            if (p == null || !ua.Get(http)!.CanSeeCircle(p.Circle)) return Results.NotFound();
            if (p.Status == LeadershipProtocolStatus.Abgeschlossen) return Results.BadRequest();
            // Beenden durch die Protokollfuehrung (ohne gesetzte Protokollfuehrung: jeder im Kreis).
            if (p.KeeperMemberId != null && !await IsManagerAsync(p.Id, http, ua, repo)) return Results.Forbid();
            var (mailed, recipients) = await svc.CloseAsync(p, DisplayName(http));
            return Results.Json(new { ok = true, mailed, recipients });
        });

        g.MapGet("/protocols/{id:int}/pdf", async (int id, HttpContext http, UserAccessService ua, LeadershipRepository repo, LeadershipService svc) =>
        {
            var p = await repo.GetProtocolAsync(id);
            if (p == null || !ua.Get(http)!.CanSeeCircle(p.Circle)) return Results.NotFound();
            return Results.File(await svc.BuildPdfAsync(p), "application/pdf");
        });
    }

    // ------------------------------------------------------------------ Helfer

    /// <summary>Protokoll, das der Nutzer sehen UND bearbeiten darf (abgeschlossen: nur Admin).</summary>
    private static async Task<LeadershipProtocol?> EditableProtocolAsync(int id, HttpContext http, UserAccessService ua, LeadershipRepository repo)
    {
        var viewer = ua.Get(http);
        var p = await repo.GetProtocolAsync(id);
        if (p == null || viewer == null || !viewer.CanSeeCircle(p.Circle)) return null;
        if (p.Status == LeadershipProtocolStatus.Abgeschlossen && !viewer.IsAdmin) return null;
        return p;
    }

    /// <summary>Protokollfuehrung dieser Sitzung oder Admin. Ohne gesetzte Protokollfuehrung: jeder im Kreis.</summary>
    private static async Task<bool> IsManagerAsync(int protocolId, HttpContext http, UserAccessService ua, LeadershipRepository repo)
    {
        var viewer = ua.Get(http); var p = await repo.GetProtocolAsync(protocolId);
        if (viewer == null || p == null) return false;
        return viewer.IsAdmin || p.KeeperMemberId == null || viewer.MemberId == p.KeeperMemberId;
    }

    private static async Task<int> ResolveCircleAsync(LeadershipRepository repo, int? requested, int? sourceItemId)
    {
        if (sourceItemId is int sid && await repo.GetItemAsync(sid) is LeadershipItem item) return item.Circle;
        return requested is int c && LeadershipCircles.IsValid(c) ? c : LeadershipCircles.Large;
    }

    private static string DisplayName(HttpContext http)
    {
        var first = http.User.FindFirst("FirstName")?.Value;
        var last = http.User.FindFirst("LastName")?.Value;
        var full = $"{first} {last}".Trim();
        return full.Length > 0 ? full : http.User.Identity?.Name ?? "";
    }

    private static async Task<Dictionary<int, string>> MemberNamesAsync(MemberRepository members) =>
        (await members.GetAllAsync()).ToDictionary(m => m.Id, m => $"{m.FirstName} {m.LastName}");

    private static async Task<Dictionary<int, DateTime>> ProtocolDatesAsync(LeadershipRepository repo, CalendarRepository calendar)
    {
        var result = new Dictionary<int, DateTime>();
        foreach (var p in await repo.GetProtocolsAsync())
            if (await calendar.GetEventAsync(p.CalendarEventId) is CalendarEvent e) result[p.Id] = e.StartTime;
        return result;
    }
}

public record TaskRequest(string? Title, string? Description, int? Circle, int? AssigneeMemberId, DateTime? DueDate, int? SourceItemId = null);
public record ItemRequest(string? Title, int? OwnerMemberId, string? Notes = null, string? Decision = null, string? Status = null, int? Section = null,
    int? TopicId = null, int? AreaId = null);
public record TopicRequest(string? Title, int? Circle, int? Kind, bool? IsArchived = null);
public record TopicEntryRequest(string? Title, string? Notes, int? TopicId = null);
public record ReorderRequest(List<int>? Ids);
public record AttendanceRequest(int MemberId, int State);
public record KeeperRequest(int? MemberId);
public record NoteRequest(string? Note);
