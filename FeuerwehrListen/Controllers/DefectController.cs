using Microsoft.AspNetCore.Mvc;
using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;
using FeuerwehrListen.DTOs;
using FeuerwehrListen.Services;

namespace FeuerwehrListen.Controllers;

[ApiController]
[Route("api/defects")]
public class DefectController : ControllerBase
{
    private readonly DefectRepository _defectRepo;
    private readonly MemberRepository _memberRepo;
    private readonly VehicleRepository _vehicleRepo;
    private readonly ListNotificationService _notificationService;

    public DefectController(
        DefectRepository defectRepo,
        MemberRepository memberRepo,
        VehicleRepository vehicleRepo,
        ListNotificationService notificationService)
    {
        _defectRepo = defectRepo;
        _memberRepo = memberRepo;
        _vehicleRepo = vehicleRepo;
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<Defect>>>> GetAll(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        DefectStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<DefectStatus>(status, true, out var parsed))
            statusFilter = parsed;

        var defects = await _defectRepo.GetPagedAsync(page, pageSize, statusFilter);
        var count = await _defectRepo.GetCountAsync(statusFilter);

        return Ok(new ApiResponse<List<Defect>>
        {
            Success = true,
            Message = $"Total: {count}",
            Data = defects
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<DefectDetailResponse>>> GetById(int id)
    {
        var defect = await _defectRepo.GetByIdAsync(id);
        if (defect == null)
            return NotFound(new ApiError { Error = "Defect not found" });

        var statusChanges = await _defectRepo.GetStatusChangesAsync(id);

        var response = new DefectDetailResponse
        {
            Defect = defect,
            StatusChanges = statusChanges
        };

        return Ok(new ApiResponse<DefectDetailResponse>
        {
            Success = true,
            Data = response
        });
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Defect>>> Create([FromBody] CreateDefectRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(new ApiError { Error = "Description is required" });

        if (string.IsNullOrWhiteSpace(request.ReportedByName))
            return BadRequest(new ApiError { Error = "ReportedByName is required" });

        string? vehicleName = null;
        if (request.VehicleId.HasValue)
        {
            var vehicles = await _vehicleRepo.GetActiveAsync();
            var vehicle = vehicles.FirstOrDefault(v => v.Id == request.VehicleId.Value);
            vehicleName = vehicle?.Name;
        }

        var defect = new Defect
        {
            Description = request.Description.Trim(),
            VehicleId = request.VehicleId,
            VehicleName = vehicleName,
            CustomVehicle = request.CustomVehicle?.Trim(),
            Status = DefectStatus.Open,
            ReportedByName = request.ReportedByName.Trim(),
            ReportedAt = DateTime.Now
        };

        defect.Id = await _defectRepo.CreateAsync(defect);

        return CreatedAtAction(nameof(GetById), new { id = defect.Id }, new ApiResponse<Defect>
        {
            Success = true,
            Message = "Defect created successfully",
            Data = defect
        });
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult<ApiResponse<Defect>>> UpdateStatus(int id, [FromBody] UpdateDefectStatusRequest request)
    {
        var defect = await _defectRepo.GetByIdAsync(id);
        if (defect == null)
            return NotFound(new ApiError { Error = "Defect not found" });

        if (string.IsNullOrWhiteSpace(request.ChangedByName))
            return BadRequest(new ApiError { Error = "ChangedByName is required" });

        if (!Enum.TryParse<DefectStatus>(request.NewStatus, true, out var newStatus))
            return BadRequest(new ApiError { Error = "Invalid status", Details = "Valid values: Open, InProgress, Done" });

        var oldStatus = defect.Status;
        defect.Status = newStatus;

        if (newStatus == DefectStatus.Done)
        {
            defect.ResolvedByName = request.ChangedByName.Trim();
            defect.ResolvedAt = DateTime.Now;
        }

        await _defectRepo.UpdateAsync(defect);

        var statusChange = new DefectStatusChange
        {
            DefectId = id,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedByName = request.ChangedByName.Trim(),
            ChangedAt = DateTime.Now,
            Comment = request.Comment?.Trim()
        };
        await _defectRepo.AddStatusChangeAsync(statusChange);

        return Ok(new ApiResponse<Defect>
        {
            Success = true,
            Message = "Status updated successfully",
            Data = defect
        });
    }
}
