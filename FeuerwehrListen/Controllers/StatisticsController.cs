using Microsoft.AspNetCore.Mvc;
using FeuerwehrListen.Services;
using FeuerwehrListen.DTOs;
using FeuerwehrListen.Models;

namespace FeuerwehrListen.Controllers;

[ApiController]
[Route("api/statistics")]
public class StatisticsController : ControllerBase
{
    private readonly StatisticsService _statisticsService;

    public StatisticsController(StatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<ApiResponse<ListStatistics>>> GetOverview()
    {
        var stats = await _statisticsService.GetListStatisticsAsync();
        return Ok(new ApiResponse<ListStatistics>
        {
            Success = true,
            Data = stats
        });
    }

    [HttpGet("top-participants")]
    public async Task<ActionResult<ApiResponse<List<TopParticipant>>>> GetTopParticipants([FromQuery] int limit = 10)
    {
        var participants = await _statisticsService.GetTopParticipantsAsync(limit);
        return Ok(new ApiResponse<List<TopParticipant>>
        {
            Success = true,
            Data = participants
        });
    }

    [HttpGet("member-statistics")]
    public async Task<ActionResult<ApiResponse<List<MemberStatistics>>>> GetMemberStatistics()
    {
        var stats = await _statisticsService.GetMemberStatisticsAsync();
        return Ok(new ApiResponse<List<MemberStatistics>>
        {
            Success = true,
            Data = stats
        });
    }

    [HttpGet("trend-data")]
    public async Task<ActionResult<ApiResponse<List<TrendData>>>> GetTrendData([FromQuery] int months = 12)
    {
        var trendData = await _statisticsService.GetTrendDataAsync(months);
        return Ok(new ApiResponse<List<TrendData>>
        {
            Success = true,
            Data = trendData
        });
    }

    [HttpGet("vehicle-statistics")]
    public async Task<ActionResult<ApiResponse<List<VehicleStatistics>>>> GetVehicleStatistics()
    {
        var stats = await _statisticsService.GetVehicleStatisticsAsync();
        return Ok(new ApiResponse<List<VehicleStatistics>>
        {
            Success = true,
            Data = stats
        });
    }

    [HttpGet("function-statistics")]
    public async Task<ActionResult<ApiResponse<List<FunctionStatistics>>>> GetFunctionStatistics()
    {
        var stats = await _statisticsService.GetFunctionStatisticsAsync();
        return Ok(new ApiResponse<List<FunctionStatistics>>
        {
            Success = true,
            Data = stats
        });
    }

    [HttpGet("breathing-apparatus-statistics")]
    public async Task<ActionResult<ApiResponse<BreathingApparatusStatistics>>> GetBreathingApparatusStatistics()
    {
        var stats = await _statisticsService.GetBreathingApparatusStatisticsAsync();
        return Ok(new ApiResponse<BreathingApparatusStatistics>
        {
            Success = true,
            Data = stats
        });
    }
}

