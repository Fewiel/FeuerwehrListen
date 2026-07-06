using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace FeuerwehrListen.Controllers;

// HINWEIS: Dieser Endpoint wird aktuell NICHT mehr aus dem Client aufgerufen (Grep im
// Repo nach "/download/" ergab nur Doku + das statische "/downloads/"-Verzeichnis; PDFs
// laufen jetzt ueber /client-api/export/* bzw. /api/export). Er bleibt vorerst bestehen,
// ist aber gegen Reflected-File-Download / Content-Spoofing abgesichert: erzwungenes
// attachment, fixer Content-Type application/octet-stream, Groessenbegrenzung.
[Route("download")]
public class DownloadController : ControllerBase
{
    // Base64 blaeht ~33 % auf; 25 MB Nutzdaten -> ca. 34 MB Base64-Laenge.
    private const int MaxDecodedBytes = 25 * 1024 * 1024;
    private const int MaxBase64Length = (MaxDecodedBytes / 3 + 1) * 4;

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

            var base64Data = parts[1];
            if (base64Data.Length > MaxBase64Length)
            {
                return BadRequest("Data too large");
            }

            byte[] fileBytes;
            try { fileBytes = Convert.FromBase64String(base64Data); }
            catch (FormatException) { return BadRequest("Invalid base64 data"); }

            if (fileBytes.Length > MaxDecodedBytes)
            {
                return BadRequest("Data too large");
            }

            // Dateinamen bereinigen (keine Pfad-/Steuerzeichen).
            var safeName = string.Join("_", Path.GetFileName(fileName ?? "download")
                .Split(Path.GetInvalidFileNameChars()));
            if (string.IsNullOrWhiteSpace(safeName)) safeName = "download";

            // Immer als Anhang, fixer generischer Content-Type -> kein Content-Spoofing.
            var cd = new ContentDispositionHeaderValue("attachment");
            cd.SetHttpFileName(safeName);
            Response.Headers[HeaderNames.ContentDisposition] = cd.ToString();
            Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";

            return File(fileBytes, "application/octet-stream");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Download error: {ex.Message}");
        }
    }
}

