using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;
using LinqToDB;

namespace FeuerwehrListen.Services;

public class ScheduledListBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduledListBackgroundService> _logger;

    public ScheduledListBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ScheduledListBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing scheduled lists");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}

