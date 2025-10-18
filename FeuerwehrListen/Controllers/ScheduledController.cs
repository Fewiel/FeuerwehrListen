using Microsoft.AspNetCore.Mvc;
using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;
using FeuerwehrListen.DTOs;

namespace FeuerwehrListen.Controllers;

[ApiController]
[Route("api/scheduled")]
public class ScheduledController : ControllerBase
{
    private readonly ScheduledListRepository _scheduledRepo;

    public ScheduledController(ScheduledListRepository scheduledRepo)
    {
        _scheduledRepo = scheduledRepo;
    }

    [HttpPost("lists")]
    public async Task<ActionResult<ApiResponse<int>>> CreateScheduledList([FromBody] CreateScheduledListRequest request)
    {
        var scheduled = new ScheduledList
        {
            Type = request.Type,
            Title = request.Title ?? string.Empty,
            Unit = request.Unit ?? string.Empty,
            Description = request.Description ?? string.Empty,
            OperationNumber = request.OperationNumber ?? string.Empty,
            Keyword = request.Keyword ?? string.Empty,
            ScheduledEventTime = request.ScheduledEventTime,
            MinutesBeforeEvent = request.MinutesBeforeEvent,
            CreatedAt = DateTime.Now,
            IsProcessed = false
        };

        var id = await _scheduledRepo.CreateAsync(scheduled);

        return Ok(new ApiResponse<int>
        {
            Success = true,
            Message = "Scheduled list created successfully",
            Data = id
        });
    }

    [HttpGet("lists")]
    public async Task<ActionResult<ApiResponse<List<ScheduledList>>>> GetPendingScheduled()
    {
        var scheduled = await _scheduledRepo.GetPendingAsync();
        return Ok(new ApiResponse<List<ScheduledList>>
        {
            Success = true,
            Data = scheduled
        });
    }
}




