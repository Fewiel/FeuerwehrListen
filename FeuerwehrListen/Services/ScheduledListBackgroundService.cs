using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;
using LinqToDB;

namespace FeuerwehrListen.Services;

public class ScheduledListBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduledListBackgroundService> _logger;
    private readonly SettingsService _settingsService;

    public ScheduledListBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ScheduledListBackgroundService> logger,
        SettingsService settingsService)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _settingsService = settingsService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScheduledListBackgroundService started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbConnection>();
                var scheduledRepo = new ScheduledListRepository(db);
                var attendanceRepo = new AttendanceListRepository(db);
                var operationRepo = new OperationListRepository(db);
                var listNotificationService = scope.ServiceProvider.GetRequiredService<ListNotificationService>();

                var dueSchedules = await scheduledRepo.GetDueAsync();
                
                _logger.LogInformation($"Checking for due scheduled lists. Found: {dueSchedules.Count}");

                foreach (var schedule in dueSchedules)
                {
                    _logger.LogInformation($"Processing scheduled list: {schedule.Title} (ID: {schedule.Id}, Type: {schedule.Type})");
                    
                    if (schedule.Type == ScheduledListType.Attendance)
                    {
                        var newList = new AttendanceList
                        {
                            Title = schedule.Title,
                            Unit = schedule.Unit,
                            Description = schedule.Description,
                            UnitNumber = schedule.UnitNumber,
                            CreatedAt = DateTime.Now,
                            Status = ListStatus.Open
                        };
                        await attendanceRepo.CreateAsync(newList);
                    }
                    else if (schedule.Type == ScheduledListType.Operation)
                    {
                        var newList = new OperationList
                        {
                            OperationNumber = schedule.OperationNumber,
                            Keyword = schedule.Keyword,
                            AlertTime = schedule.ScheduledEventTime,
                            CreatedAt = DateTime.Now,
                            Status = ListStatus.Open
                        };
                        await operationRepo.CreateAsync(newList);
                    }

                    schedule.IsProcessed = true;
                    await scheduledRepo.UpdateAsync(schedule);

                    _logger.LogInformation($"Processed scheduled list: {schedule.Title}");
                }

                // Auto-close logic
                await AutoCloseListsAsync(db, listNotificationService);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing scheduled lists");
            }

            // Kalender bewusst in einem EIGENEN try/catch: ein Fehler hier darf die
            // geplanten Listen und den Auto-Close nicht mitreissen.
            try
            {
                await ProcessCalendarAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing calendar");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    /// <summary>Zaehler, damit die Serien-Materialisierung nicht jede Minute laeuft.</summary>
    private int _calendarTicks;

    /// <summary>
    /// Kalender-Aufgaben: (1) fuer faellige Dienste die Anwesenheitsliste anlegen,
    /// (2) stuendlich die Serientermine im rollierenden Horizont auffuellen.
    ///
    /// Die Liste wird bewusst erst kurz vor dem Termin erzeugt: der Auto-Close fuer
    /// Anwesenheitslisten rechnet ab CreatedAt, eine weit im Voraus erzeugte Liste waere
    /// bei Dienstbeginn also womoeglich schon wieder geschlossen.
    /// </summary>
    private async Task ProcessCalendarAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var calendarRepo = scope.ServiceProvider.GetRequiredService<CalendarRepository>();
        var attendanceRepo = scope.ServiceProvider.GetRequiredService<AttendanceListRepository>();
        var calendarService = scope.ServiceProvider.GetRequiredService<CalendarService>();

        var now = DateTime.Now;
        var due = await calendarRepo.GetDueDienstEventsAsync(now, now.AddDays(1));

        foreach (var ev in due)
        {
            try
            {
                var listId = await attendanceRepo.CreateAsync(new AttendanceList
                {
                    Title = ev.Title,
                    Unit = ev.UnitNumber is int un ? _settingsService.GetUnitLabel(un) : "Allgemein",
                    // Unit und Description sind in der DB NOT NULL - null wuerde die Zeile ablehnen.
                    Description = ev.Description ?? string.Empty,
                    UnitNumber = ev.UnitNumber,
                    CreatedAt = DateTime.Now,
                    Status = ListStatus.Open
                });

                // Rueckverweis ist zugleich der Duplikatschutz - idempotent, anders als ein
                // separates IsProcessed-Flag, das bei einem Absturz dazwischen Doppellisten erzeugt.
                ev.AttendanceListId = listId;
                await calendarRepo.UpdateEventAsync(ev);

                _logger.LogInformation("Anwesenheitsliste {ListId} fuer Dienst '{Title}' angelegt.", listId, ev.Title);
            }
            catch (Exception ex)
            {
                // Einzelner Termin darf die uebrigen nicht blockieren.
                _logger.LogError(ex, "Anwesenheitsliste fuer Kalendertermin {EventId} fehlgeschlagen.", ev.Id);
            }
        }

        // Serien nur stuendlich auffuellen - jede Minute waere unnoetige Last.
        if (_calendarTicks++ % 60 == 0)
        {
            var created = await calendarService.MaterializeAllSeriesAsync();
            if (created > 0) _logger.LogInformation("{Count} Serientermine materialisiert.", created);
        }
    }

    private async Task AutoCloseListsAsync(AppDbConnection db, ListNotificationService listNotificationService)
    {
        var now = DateTime.Now;

        // Auto-close attendance lists
        var attendanceMinutes = _settingsService.GetAutoCloseMinutes(SettingKeys.AutoCloseAttendance);
        if (attendanceMinutes > 0)
        {
            var cutoff = now.AddMinutes(-attendanceMinutes);
            var openAttendance = await db.AttendanceLists
                .Where(l => l.Status == ListStatus.Open && l.CreatedAt <= cutoff)
                .ToListAsync();

            foreach (var list in openAttendance)
            {
                list.Status = ListStatus.Closed;
                list.ClosedAt = now;
                await db.UpdateAsync(list);
                await listNotificationService.NotifyAttendanceClosedAsync(list);
                _logger.LogInformation("Auto-closed attendance list: {Title} (ID: {Id})", list.Title, list.Id);
            }
        }

        // Auto-close operation lists
        var operationMinutes = _settingsService.GetAutoCloseMinutes(SettingKeys.AutoCloseOperations);
        if (operationMinutes > 0)
        {
            var cutoff = now.AddMinutes(-operationMinutes);
            var openOperations = await db.OperationLists
                .Where(l => l.Status == ListStatus.Open && l.CreatedAt <= cutoff)
                .ToListAsync();

            foreach (var list in openOperations)
            {
                list.Status = ListStatus.Closed;
                list.ClosedAt = now;
                await db.UpdateAsync(list);
                await listNotificationService.NotifyOperationClosedAsync(list);
                _logger.LogInformation("Auto-closed operation list: {OperationNumber} (ID: {Id})", list.OperationNumber, list.Id);
            }
        }

        // Auto-close fire safety watches
        // Note: FSW uses EventDateTime (not CreatedAt which doesn't exist on this model).
        // This means watches are closed N minutes after the event time, which is the intended behavior.
        var fswMinutes = _settingsService.GetAutoCloseMinutes(SettingKeys.AutoCloseFireSafetyWatch);
        if (fswMinutes > 0)
        {
            var cutoff = now.AddMinutes(-fswMinutes);
            var openWatches = await db.FireSafetyWatches
                .Where(w => w.Status == ListStatus.Open && w.EventDateTime <= cutoff)
                .ToListAsync();

            foreach (var watch in openWatches)
            {
                watch.Status = ListStatus.Closed;
                watch.ClosedAt = now;
                await db.UpdateAsync(watch);
                await listNotificationService.NotifyFireSafetyWatchClosedAsync(watch);
                _logger.LogInformation("Auto-closed fire safety watch: {Name} (ID: {Id})", watch.Name, watch.Id);
            }
        }
    }
}

