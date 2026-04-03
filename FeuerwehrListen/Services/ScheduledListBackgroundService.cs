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
                await AutoCloseListsAsync(db);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing scheduled lists");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task AutoCloseListsAsync(AppDbConnection db)
    {
        var now = DateTime.Now;

        // Auto-close attendance lists
        var attendanceMinutes = _settingsService.GetAutoCloseMinutes("AutoClose.AttendanceMinutes");
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
                _logger.LogInformation("Auto-closed attendance list: {Title} (ID: {Id})", list.Title, list.Id);
            }
        }

        // Auto-close operation lists
        var operationMinutes = _settingsService.GetAutoCloseMinutes("AutoClose.OperationMinutes");
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
                _logger.LogInformation("Auto-closed operation list: {OperationNumber} (ID: {Id})", list.OperationNumber, list.Id);
            }
        }

        // Auto-close fire safety watches
        var fswMinutes = _settingsService.GetAutoCloseMinutes("AutoClose.FireSafetyWatchMinutes");
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
                _logger.LogInformation("Auto-closed fire safety watch: {Name} (ID: {Id})", watch.Name, watch.Id);
            }
        }
    }
}

