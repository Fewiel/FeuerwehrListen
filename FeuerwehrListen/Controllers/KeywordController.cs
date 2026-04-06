using Microsoft.AspNetCore.Mvc;
using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;
using FeuerwehrListen.DTOs;

namespace FeuerwehrListen.Controllers;

[ApiController]
[Route("api/keywords")]
public class KeywordController : ControllerBase
{
    private readonly KeywordRepository _keywordRepo;

    public KeywordController(KeywordRepository keywordRepo)
    {
        _keywordRepo = keywordRepo;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<Keyword>>>> GetAll()
    {
        var keywords = await _keywordRepo.GetAllAsync();
        return Ok(new ApiResponse<List<Keyword>>
        {
            Success = true,
            Data = keywords
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<Keyword>>> GetById(int id)
    {
        var keyword = await _keywordRepo.GetByIdAsync(id);
        if (keyword == null)
            return NotFound(new ApiError { Error = "Keyword not found" });

        return Ok(new ApiResponse<Keyword>
        {
            Success = true,
            Data = keyword
        });
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Keyword>>> Create([FromBody] CreateKeywordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new ApiError { Error = "Name is required" });

        var existing = await _keywordRepo.GetByNameAsync(request.Name.Trim());
        if (existing != null)
            return Conflict(new ApiError { Error = "Keyword already exists", Details = $"Keyword '{existing.Name}' already exists with Id {existing.Id}" });

        var keyword = new Keyword
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.Now
        };

        keyword.Id = await _keywordRepo.CreateAsync(keyword);

        return CreatedAtAction(nameof(GetById), new { id = keyword.Id }, new ApiResponse<Keyword>
        {
            Success = true,
            Message = "Keyword created successfully",
            Data = keyword
        });
    }

    [HttpGet("search")]
    public async Task<ActionResult<ApiResponse<List<Keyword>>>> Search([FromQuery] string q, [FromQuery] int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new ApiError { Error = "Search query cannot be empty" });

        var keywords = await _keywordRepo.SearchAsync(q, limit);
        return Ok(new ApiResponse<List<Keyword>>
        {
            Success = true,
            Data = keywords
        });
    }
}
