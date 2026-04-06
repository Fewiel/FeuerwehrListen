using Microsoft.AspNetCore.Mvc;
using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;
using FeuerwehrListen.DTOs;
using FeuerwehrListen.Services;

namespace FeuerwehrListen.Controllers;

[ApiController]
[Route("api/firesafetywatches")]
public class FireSafetyWatchController : ControllerBase
{
    private readonly FireSafetyWatchRepository _watchRepo;
    private readonly FireSafetyWatchEntryRepository _entryRepo;
    private readonly FireSafetyWatchRequirementRepository _requirementRepo;
    private readonly MemberRepository _memberRepo;
    private readonly ListNotificationService _notificationService;

    public FireSafetyWatchController(
        FireSafetyWatchRepository watchRepo,
        FireSafetyWatchEntryRepository entryRepo,
        FireSafetyWatchRequirementRepository requirementRepo,
        MemberRepository memberRepo,
        ListNotificationService notificationService)
    {
        _watchRepo = watchRepo;
        _entryRepo = entryRepo;
        _requirementRepo = requirementRepo;
        _memberRepo = memberRepo;
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<FireSafetyWatchDto>>>> GetAll()
    {
        var watches = await _watchRepo.GetAllWithStatusAsync();
        var open = watches.Where(w => !w.IsArchived && w.Status == ListStatus.Open).ToList();
        return Ok(new ApiResponse<List<FireSafetyWatchDto>>
        {
            Success = true,
            Data = open
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<FireSafetyWatchDetailResponse>>> GetById(int id)
    {
        var watch = await _watchRepo.GetByIdAsync(id);
        if (watch == null)
            return NotFound(new ApiError { Error = "Fire safety watch not found" });

        var requirements = await _requirementRepo.GetByWatchIdAsync(id);
        var entries = await _entryRepo.GetByWatchIdAsync(id);

        var response = new FireSafetyWatchDetailResponse
        {
            Id = watch.Id,
            Name = watch.Name,
            Location = watch.Location,
            EventDateTime = watch.EventDateTime,
            Status = watch.Status.ToString(),
            ClosedAt = watch.ClosedAt,
            Requirements = requirements.Select(r => new FireSafetyWatchRequirementResponse
            {
                Id = r.Id,
                Function = r.FunctionDef?.Name ?? "",
                Amount = r.Amount,
                Vehicle = r.Vehicle?.Name
            }).ToList(),
            Entries = entries.Select(e => new FireSafetyWatchEntryResponse
            {
                Id = e.Id,
                RequirementId = e.RequirementId,
                MemberId = e.MemberId,
                MemberName = e.Member != null ? $"{e.Member.FirstName} {e.Member.LastName}" : ""
            }).ToList()
        };

        return Ok(new ApiResponse<FireSafetyWatchDetailResponse>
        {
            Success = true,
            Data = response
        });
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<int>>> Create([FromBody] CreateFireSafetyWatchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new ApiError { Error = "Name is required" });

        var watch = new FireSafetyWatch
        {
            Name = request.Name.Trim(),
            Location = request.Location?.Trim() ?? "",
            EventDateTime = request.EventDateTime,
            Status = ListStatus.Open
        };

        var requirements = (request.Requirements ?? new()).Select(r => new FireSafetyWatchRequirement
        {
            FunctionDefId = r.FunctionDefId,
            Amount = r.Amount,
            VehicleId = r.VehicleId
        }).ToList();

        await _watchRepo.InsertFireSafetyWatchWithRequirements(watch, requirements);

        return CreatedAtAction(nameof(GetById), new { id = watch.Id }, new ApiResponse<int>
        {
            Success = true,
            Message = "Fire safety watch created successfully",
            Data = watch.Id
        });
    }

    [HttpPost("{id}/entries")]
    public async Task<ActionResult<ApiResponse<string>>> AddEntry(int id, [FromBody] AddFireSafetyWatchEntryRequest request)
    {
        var watch = await _watchRepo.GetByIdAsync(id);
        if (watch == null)
            return NotFound(new ApiError { Error = "Fire safety watch not found" });

        if (watch.Status != ListStatus.Open)
            return BadRequest(new ApiError { Error = "Watch is closed" });

        var member = await _memberRepo.FindByNameOrNumberAsync(request.MemberNumberOrName);
        if (member == null)
            return BadRequest(new ApiError { Error = "Member not found", Details = $"No active member found for: {request.MemberNumberOrName}" });

        var entry = new FireSafetyWatchEntry
        {
            FireSafetyWatchId = id,
            RequirementId = request.RequirementId,
            MemberId = member.Id
        };

        await _entryRepo.InsertAsync(entry);

        return Ok(new ApiResponse<string>
        {
            Success = true,
            Message = "Entry added successfully"
        });
    }

    [HttpDelete("{id}/entries/{entryId}")]
    public async Task<ActionResult<ApiResponse<string>>> DeleteEntry(int id, int entryId)
    {
        var watch = await _watchRepo.GetByIdAsync(id);
        if (watch == null)
            return NotFound(new ApiError { Error = "Fire safety watch not found" });

        if (watch.Status != ListStatus.Open)
            return BadRequest(new ApiError { Error = "Cannot delete entry from closed watch" });

        await _entryRepo.DeleteAsync(entryId);

        return Ok(new ApiResponse<string>
        {
            Success = true,
            Message = "Entry deleted successfully"
        });
    }

    [HttpPost("{id}/close")]
    public async Task<ActionResult<ApiResponse<string>>> Close(int id)
    {
        var watch = await _watchRepo.GetByIdAsync(id);
        if (watch == null)
            return NotFound(new ApiError { Error = "Fire safety watch not found" });

        if (watch.Status == ListStatus.Closed)
            return BadRequest(new ApiError { Error = "Watch is already closed" });

        watch.Status = ListStatus.Closed;
        watch.ClosedAt = DateTime.Now;
        await _watchRepo.UpdateAsync(watch);
        await _notificationService.NotifyFireSafetyWatchClosedAsync(watch);

        return Ok(new ApiResponse<string>
        {
            Success = true,
            Message = "Fire safety watch closed successfully",
            Data = watch.ClosedAt?.ToString("yyyy-MM-dd HH:mm:ss")
        });
    }
}
