using Microsoft.AspNetCore.Mvc;
using FeuerwehrListen.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FeuerwehrListen.DTOs;

namespace FeuerwehrListen.Controllers;

[ApiController]
[Route("api/export")]
[Authorize]
public class ExportController : ControllerBase
{
    private readonly PdfExportService _pdfService;
    private readonly DownloadTokenService _tokenService;

    public ExportController(PdfExportService pdfService, DownloadTokenService tokenService)
    {
        _pdfService = pdfService;
        _tokenService = tokenService;
    }

    [AllowAnonymous]
    [HttpGet("attendance/{listId}/pdf")]
    public async Task<IActionResult> ExportAttendanceListPdf(int listId, [FromQuery] string? token)
    {
        try
        {
            var expectedPath = $"/api/export/attendance/{listId}/pdf";
            if (string.IsNullOrEmpty(token) || !_tokenService.ValidateAndConsume(token, expectedPath))
            {
                return Unauthorized("Ungültiges oder fehlendes Download-Token");
            }
            var pdfBytes = await _pdfService.ExportAttendanceListAsync(listId);
            var fileName = $"Anwesenheitsliste_{listId}_{DateTime.Now:yyyyMMdd}.pdf";
            
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ApiError { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError { Error = "Export failed", Details = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpGet("operation/{listId}/pdf")]
    public async Task<IActionResult> ExportOperationListPdf(int listId, [FromQuery] string? token)
    {
        try
        {
            var expectedPath = $"/api/export/operation/{listId}/pdf";
            if (string.IsNullOrEmpty(token) || !_tokenService.ValidateAndConsume(token, expectedPath))
            {
                return Unauthorized("Ungültiges oder fehlendes Download-Token");
            }
            var pdfBytes = await _pdfService.ExportOperationListAsync(listId);
            var fileName = $"Einsatzliste_{listId}_{DateTime.Now:yyyyMMdd}.pdf";
            
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ApiError { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError { Error = "Export failed", Details = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpGet("statistics/pdf")]
    public async Task<IActionResult> ExportStatisticsPdf([FromQuery] string? token)
    {
        try
        {
            var expectedPath = "/api/export/statistics/pdf";
            if (string.IsNullOrEmpty(token) || !_tokenService.ValidateAndConsume(token, expectedPath))
            {
                return Unauthorized("Ungültiges oder fehlendes Download-Token");
            }
            var pdfBytes = await _pdfService.ExportStatisticsReportAsync();
            var fileName = $"Statistikbericht_{DateTime.Now:yyyyMMdd}.pdf";
            
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError { Error = "Export failed", Details = ex.Message });
        }
    }
}


