using Microsoft.AspNetCore.Mvc;
using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;
using FeuerwehrListen.DTOs;

namespace FeuerwehrListen.Controllers;

[ApiController]
[Route("api/attendance")]
public class AttendanceController : ControllerBase
{
    private readonly AttendanceListRepository _listRepo;
    private readonly AttendanceEntryRepository _entryRepo;
    private readonly MemberRepository _memberRepo;

    public AttendanceController(
        AttendanceListRepository listRepo,
        AttendanceEntryRepository entryRepo,
        MemberRepository memberRepo)
    {
        _listRepo = listRepo;
        _entryRepo = entryRepo;
        _memberRepo = memberRepo;
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
        var list = new AttendanceList
        {
            Title = request.Title,
            Unit = request.Unit,
            Description = request.Description,
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
        var list = await _listRepo.GetByIdAsync(listId);
        if (list == null)
            return NotFound(new ApiError { Error = "List not found" });

        if (list.Status != ListStatus.Open)
            return BadRequest(new ApiError { Error = "List is closed" });

        var member = await _memberRepo.FindByNameOrNumberAsync(request.MemberNumberOrName);
        if (member == null)
            return BadRequest(new ApiError { Error = "Member not found", Details = $"No active member found for: {request.MemberNumberOrName}" });

        var entry = new AttendanceEntry
        {
            AttendanceListId = listId,
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
                ListId = listId,
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


