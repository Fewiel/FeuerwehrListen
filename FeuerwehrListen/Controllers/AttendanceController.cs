using Microsoft.AspNetCore.Mvc;
using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;
using FeuerwehrListen.DTOs;
using FeuerwehrListen.Services;

namespace FeuerwehrListen.Controllers;

[ApiController]
[Route("api/attendance")]
public class AttendanceController : ControllerBase
{
    private readonly AttendanceListRepository _listRepo;
    private readonly AttendanceEntryRepository _entryRepo;
    private readonly MemberRepository _memberRepo;
    private readonly UnitAssignmentService _unitAssignmentService;
    private readonly ListNotificationService _listNotificationService;

    public AttendanceController(
        AttendanceListRepository listRepo,
        AttendanceEntryRepository entryRepo,
        MemberRepository memberRepo,
        UnitAssignmentService unitAssignmentService,
        ListNotificationService listNotificationService)
    {
        _listRepo = listRepo;
        _entryRepo = entryRepo;
        _memberRepo = memberRepo;
        _unitAssignmentService = unitAssignmentService;
        _listNotificationService = listNotificationService;
    }

    [HttpGet("lists")]
    public async Task<ActionResult<ApiResponse<List<ListResponse>>>> GetOpenLists()
    {
        var lists = await _listRepo.GetOpenAsync();
        var response = lists.Select(l => new ListResponse
        {
            Id = l.Id,
            Title = l.Title,
            CreatedAt = l.CreatedAt
        }).ToList();

        return Ok(new ApiResponse<List<ListResponse>>
        {
            Success = true,
            Data = response
        });
    }

    [HttpGet("lists/{id}")]
    public async Task<ActionResult<ApiResponse<AttendanceList>>> GetList(int id)
    {
        var list = await _listRepo.GetByIdAsync(id);
        if (list == null)
            return NotFound(new ApiError { Error = "List not found" });

        return Ok(new ApiResponse<AttendanceList>
        {
            Success = true,
            Data = list
        });
    }

    [HttpPost("lists")]
    public async Task<ActionResult<ApiResponse<ListResponse>>> CreateList([FromBody] CreateAttendanceListRequest request)
    {
        if (request.UnitNumber.HasValue && (request.UnitNumber < 1 || request.UnitNumber > 9))
            return BadRequest(new ApiError { Error = "UnitNumber must be between 1 and 9" });

        var list = new AttendanceList
        {
            Title = request.Title,
            Unit = request.Unit,
            Description = request.Description,
            UnitNumber = request.UnitNumber,
            CreatedAt = DateTime.Now,
            Status = ListStatus.Open
        };

        var id = await _listRepo.CreateAsync(list);
        list.Id = id;

        return CreatedAtAction(nameof(GetList), new { id }, new ApiResponse<ListResponse>
        {
            Success = true,
            Message = "List created successfully",
            Data = new ListResponse
            {
                Id = id,
                Title = list.Title,
                CreatedAt = list.CreatedAt
            }
        });
    }

    [HttpGet("lists/{listId}/entries")]
    public async Task<ActionResult<ApiResponse<List<EntryResponse>>>> GetEntries(int listId)
    {
        var list = await _listRepo.GetByIdAsync(listId);
        if (list == null)
            return NotFound(new ApiError { Error = "List not found" });

        var entries = await _entryRepo.GetByListIdAsync(listId);
        var response = entries.Select(e => new EntryResponse
        {
            Id = e.Id,
            ListId = e.AttendanceListId,
            NameOrId = e.NameOrId,
            EnteredAt = e.EnteredAt,
            IsExcused = e.IsExcused
        }).ToList();

        return Ok(new ApiResponse<List<EntryResponse>>
        {
            Success = true,
            Data = response
        });
    }

    [HttpPost("lists/{listId}/entries")]
    public async Task<ActionResult<ApiResponse<EntryResponse>>> AddEntry(int listId, [FromBody] AddAttendanceEntryRequest request)
    {
        var sourceList = await _listRepo.GetByIdAsync(listId);
        if (sourceList == null)
            return NotFound(new ApiError { Error = "List not found" });

        if (sourceList.Status != ListStatus.Open)
            return BadRequest(new ApiError { Error = "List is closed" });

        var member = await _memberRepo.FindByNameOrNumberAsync(request.MemberNumberOrName);
        if (member == null)
            return BadRequest(new ApiError { Error = "Member not found", Details = $"No active member found for: {request.MemberNumberOrName}" });

        var targetListId = listId;
        if (sourceList.UnitNumber.HasValue)
        {
            var resolvedUnit = _unitAssignmentService.ResolveUnitNumber(member);
            if (resolvedUnit.HasValue && resolvedUnit.Value != sourceList.UnitNumber.Value)
            {
                var unitList = await _listRepo.GetOpenByUnitNumberAsync(resolvedUnit.Value);
                if (unitList == null)
                {
                    return BadRequest(new ApiError
                    {
                        Error = "No open attendance list for member unit",
                        Details = $"No open attendance list for unit {resolvedUnit.Value}."
                    });
                }

                targetListId = unitList.Id;
            }
        }

        var entry = new AttendanceEntry
        {
            AttendanceListId = targetListId,
            NameOrId = $"{member.FirstName} {member.LastName} ({member.MemberNumber})",
            EnteredAt = DateTime.Now,
            IsExcused = request.IsExcused
        };

        var id = await _entryRepo.CreateAsync(entry);

        return Ok(new ApiResponse<EntryResponse>
        {
            Success = true,
            Message = "Entry added successfully",
            Data = new EntryResponse
            {
                Id = id,
                ListId = targetListId,
                NameOrId = entry.NameOrId,
                EnteredAt = entry.EnteredAt,
                IsExcused = entry.IsExcused
            }
        });
    }

    [HttpPost("lists/{listId}/close")]
    public async Task<ActionResult<ApiResponse<string>>> CloseList(int listId)
    {
        var list = await _listRepo.GetByIdAsync(listId);
        if (list == null)
            return NotFound(new ApiError { Error = "List not found" });

        if (list.Status == ListStatus.Closed)
            return BadRequest(new ApiError { Error = "List is already closed" });

        list.Status = ListStatus.Closed;
        list.ClosedAt = DateTime.Now;
        await _listRepo.UpdateAsync(list);
        await _listNotificationService.NotifyAttendanceClosedAsync(list);

        return Ok(new ApiResponse<string>
        {
            Success = true,
            Message = "List closed successfully",
            Data = list.ClosedAt?.ToString("yyyy-MM-dd HH:mm:ss")
        });
    }

    [HttpDelete("lists/{listId}/entries/{entryId}")]
    public async Task<ActionResult<ApiResponse<string>>> DeleteEntry(int listId, int entryId)
    {
        var list = await _listRepo.GetByIdAsync(listId);
        if (list == null)
            return NotFound(new ApiError { Error = "List not found" });

        if (list.Status != ListStatus.Open)
            return BadRequest(new ApiError { Error = "Cannot delete entry from closed list" });

        await _entryRepo.DeleteAsync(entryId);

        return Ok(new ApiResponse<string>
        {
            Success = true,
            Message = "Entry deleted successfully"
        });
    }
}


