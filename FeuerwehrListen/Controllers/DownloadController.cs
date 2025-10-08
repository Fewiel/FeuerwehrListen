using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace FeuerwehrListen.Controllers;

[Route("download")]
public class DownloadController : ControllerBase
{
    [HttpGet("{fileName}")]
    public IActionResult DownloadFile(string fileName, [FromQuery] string data)
    {
        try
        {
            if (string.IsNullOrEmpty(data))
            {
                return BadRequest("No data provided");
            }

            var parts = data.Split(',');
            if (parts.Length != 2)
            {
                return BadRequest("Invalid data format");
            }

            var header = parts[0];
            var base64Data = parts[1];

            var contentType = "application/octet-stream";
            if (header.Contains("application/pdf"))
            {
                contentType = "application/pdf";
            }
            else if (header.Contains("text/plain"))
            {
                contentType = "text/plain";
            }

            var fileBytes = Convert.FromBase64String(base64Data);

            return File(fileBytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Download error: {ex.Message}");
        }
    }
}

