using Microsoft.AspNetCore.Mvc;
using FeuerwehrListen.Repositories;
using FeuerwehrListen.DTOs;

namespace FeuerwehrListen.Controllers;

[ApiController]
[Route("api/members")]
public class MemberController : ControllerBase
{
    private readonly MemberRepository _memberRepo;

    public MemberController(MemberRepository memberRepo)
    {
        _memberRepo = memberRepo;
    }

    [HttpGet("search")]
    public async Task<ActionResult<ApiResponse<MemberResponse>>> SearchMember([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new ApiError { Error = "Search query cannot be empty" });

        var member = await _memberRepo.FindByNameOrNumberAsync(q);
        if (member == null)
            return NotFound(new ApiError { Error = "Member not found", Details = $"No active member found for: {q}" });

        var response = new MemberResponse
        {
            Id = member.Id,
            MemberNumber = member.MemberNumber,
            FirstName = member.FirstName,
            LastName = member.LastName
        };

        return Ok(new ApiResponse<MemberResponse>
        {
            Success = true,
            Data = response
        });
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<MemberResponse>>>> GetAllMembers()
    {
        var members = await _memberRepo.GetActiveAsync();
        var response = members.Select(m => new MemberResponse
        {
            Id = m.Id,
            MemberNumber = m.MemberNumber,
            FirstName = m.FirstName,
            LastName = m.LastName
        }).ToList();

        return Ok(new ApiResponse<List<MemberResponse>>
        {
            Success = true,
            Data = response
        });
    }
}


