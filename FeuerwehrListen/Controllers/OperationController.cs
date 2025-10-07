using Microsoft.AspNetCore.Mvc;
using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;
using FeuerwehrListen.DTOs;

namespace FeuerwehrListen.Controllers;

[ApiController]
[Route("api/operation")]
public class OperationController : ControllerBase
{
    private readonly OperationListRepository _listRepo;
    private readonly OperationEntryRepository _entryRepo;
    private readonly MemberRepository _memberRepo;
    private readonly VehicleRepository _vehicleRepo;

    public OperationController(
        OperationListRepository listRepo,
        OperationEntryRepository entryRepo,
        MemberRepository memberRepo,
        VehicleRepository vehicleRepo)
    {
        _listRepo = listRepo;
        _entryRepo = entryRepo;
        _memberRepo = memberRepo;
        _vehicleRepo = vehicleRepo;
    }

    [HttpGet("lists")]
    public async Task<ActionResult<ApiResponse<List<ListResponse>>>> GetOpenLists()
    {
        var lists = await _listRepo.GetOpenAsync();
        var response = lists.Select(l => new ListResponse
        {
            Id = l.Id,
            Title = l.OperationNumber,
            CreatedAt = l.CreatedAt
        }).ToList();

        return Ok(new ApiResponse<List<ListResponse>>
        {
            Success = true,
            Data = response
        });
    }

    [HttpGet("lists/{id}")]
    public async Task<ActionResult<ApiResponse<OperationList>>> GetList(int id)
    {
        var list = await _listRepo.GetByIdAsync(id);
        if (list == null)
            return NotFound(new ApiError { Error = "List not found" });

        return Ok(new ApiResponse<OperationList>
        {
            Success = true,
            Data = list
        });
    }

    [HttpPost("lists")]
    public async Task<ActionResult<ApiResponse<ListResponse>>> CreateList([FromBody] CreateOperationListRequest request)
    {
        var list = new OperationList
        {
            OperationNumber = request.OperationNumber,
            Keyword = request.Keyword,
            AlertTime = request.AlertTime,
            CreatedAt = DateTime.Now,
            Status = ListStatus.Open
        };

        var id = await _listRepo.CreateAsync(list);
        list.Id = id;

        return CreatedAtAction(nameof(GetList), new { id }, new ApiResponse<ListResponse>
        {
            Success = true,
            Message = "Operation list created successfully",
            Data = new ListResponse
            {
                Id = id,
                Title = list.OperationNumber,
                CreatedAt = list.CreatedAt
            }
        });
    }

    [HttpGet("lists/{listId}/entries")]
    public async Task<ActionResult<ApiResponse<List<OperationEntryResponse>>>> GetEntries(int listId)
    {
        var list = await _listRepo.GetByIdAsync(listId);
        if (list == null)
            return NotFound(new ApiError { Error = "List not found" });

        var entries = await _entryRepo.GetByListIdAsync(listId);
        var response = entries.Select(e => new OperationEntryResponse
        {
            Id = e.Id,
            ListId = e.OperationListId,
            NameOrId = e.NameOrId,
            Vehicle = e.Vehicle,
            Function = e.Function.ToString(),
            WithBreathingApparatus = e.WithBreathingApparatus,
            EnteredAt = e.EnteredAt
        }).ToList();

        return Ok(new ApiResponse<List<OperationEntryResponse>>
        {
            Success = true,
            Data = response
        });
    }

    [HttpPost("lists/{listId}/entries")]
    public async Task<ActionResult<ApiResponse<OperationEntryResponse>>> AddEntry(int listId, [FromBody] AddOperationEntryRequest request)
    {
        var list = await _listRepo.GetByIdAsync(listId);
        if (list == null)
            return NotFound(new ApiError { Error = "List not found" });

        if (list.Status != ListStatus.Open)
            return BadRequest(new ApiError { Error = "List is closed" });

        var member = await _memberRepo.FindByNameOrNumberAsync(request.MemberNumberOrName);
        if (member == null)
            return BadRequest(new ApiError { Error = "Member not found", Details = $"No active member found for: {request.MemberNumberOrName}" });

        var vehicles = await _vehicleRepo.GetActiveAsync();
        if (!vehicles.Any(v => v.Name == request.Vehicle || v.CallSign == request.Vehicle))
            return BadRequest(new ApiError { Error = "Vehicle not found", Details = $"No active vehicle found: {request.Vehicle}" });

        var entry = new OperationEntry
        {
            OperationListId = listId,
            NameOrId = $"{member.FirstName} {member.LastName} ({member.MemberNumber})",
            Vehicle = request.Vehicle,
            Function = request.Function,
            WithBreathingApparatus = request.WithBreathingApparatus,
            EnteredAt = DateTime.Now
        };

        var id = await _entryRepo.CreateAsync(entry);

        return Ok(new ApiResponse<OperationEntryResponse>
        {
            Success = true,
            Message = "Entry added successfully",
            Data = new OperationEntryResponse
            {
                Id = id,
                ListId = listId,
                NameOrId = entry.NameOrId,
                Vehicle = entry.Vehicle,
                Function = entry.Function.ToString(),
                WithBreathingApparatus = entry.WithBreathingApparatus,
                EnteredAt = entry.EnteredAt
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
            Message = "Operation list closed successfully",
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

    [HttpGet("vehicles")]
    public async Task<ActionResult<ApiResponse<List<Vehicle>>>> GetVehicles()
    {
        var vehicles = await _vehicleRepo.GetActiveAsync();
        return Ok(new ApiResponse<List<Vehicle>>
        {
            Success = true,
            Data = vehicles
        });
    }
}


