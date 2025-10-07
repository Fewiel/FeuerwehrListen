using PdfSharp.Pdf;
using PdfSharp.Drawing;
using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;
using System.Net.Http;
using System.Globalization;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace FeuerwehrListen.Services;

public class PdfExportService
{
    private readonly AttendanceListRepository _attendanceRepo;
    private readonly OperationListRepository _operationRepo;
    private readonly AttendanceEntryRepository _attendanceEntryRepo;
    private readonly OperationEntryRepository _operationEntryRepo;
    private readonly StatisticsService _statisticsService;
    private readonly OperationEntryFunctionRepository _entryFunctionRepo;
    private readonly GeocodingService _geocodingService;

    public PdfExportService(
        AttendanceListRepository attendanceRepo,
        OperationListRepository operationRepo,
        AttendanceEntryRepository attendanceEntryRepo,
        OperationEntryRepository operationEntryRepo,
        StatisticsService statisticsService,
        OperationEntryFunctionRepository entryFunctionRepo,
        GeocodingService geocodingService)
    {
        _attendanceRepo = attendanceRepo;
        _operationRepo = operationRepo;
        _attendanceEntryRepo = attendanceEntryRepo;
        _operationEntryRepo = operationEntryRepo;
        _statisticsService = statisticsService;
        _entryFunctionRepo = entryFunctionRepo;
        _geocodingService = geocodingService;
    }

    private XFont CreateFont(string fontFamily, double size, bool isBold = false)
    {
        var fontStyle = isBold ? XFontStyleEx.Bold : XFontStyleEx.Regular;
        
        try
        {
            var font = new XFont(fontFamily, size, fontStyle);
            return font;
        }
        catch (Exception ex)
        {
            try
            {
                return new XFont("Arial", size, fontStyle);
            }
            catch (Exception ex2)
            {
                return new XFont("Arial", size, XFontStyleEx.Regular);
            }
        }
    }
    private static void DrawHeader(XGraphics gfx, string title, XFont titleFont, double left, double top, double width)
    {
        gfx.DrawString(title, titleFont, XBrushes.Black, new XRect(left, top, width, 24), XStringFormats.TopLeft);
        var pen = new XPen(XColors.DarkRed, 1);
        gfx.DrawLine(pen, left, top + 26, left + width, top + 26);
    }

    private static void DrawFooter(PdfDocument doc, PdfPage page, XGraphics gfx, XFont font, double left, double bottom, double width, int pageNumber)
    {
        var pen = new XPen(XColors.LightGray, 0.5);
        gfx.DrawLine(pen, left, bottom - 18, left + width, bottom - 18);
        gfx.DrawString($"Seite {pageNumber}", font, XBrushes.Gray, new XRect(left, bottom - 16, width, 14), XStringFormats.TopRight);
    }

    private static void DrawTableHeader(XGraphics gfx, XFont font, XFont bold, double left, double y, double[] widths, string[] headers)
    {
        var bg = new XSolidBrush(XColor.FromArgb(240, 240, 240));
        var pen = new XPen(XColors.Gray, 0.4);
        double x = left;
        double rowHeight = 16;
        for (int i = 0; i < headers.Length; i++)
        {
            var rect = new XRect(x, y, widths[i], rowHeight);
            gfx.DrawRectangle(bg, rect);
            gfx.DrawRectangle(pen, rect);
            gfx.DrawString(headers[i], bold, XBrushes.Black, new XRect(x + 4, y + 2, widths[i] - 8, rowHeight), XStringFormats.TopLeft);
            x += widths[i];
        }
    }

    private static void DrawTableRow(XGraphics gfx, XFont font, double left, double y, double[] widths, string[] values)
    {
        var pen = new XPen(XColors.LightGray, 0.3);
        double x = left;
        double rowHeight = 16;
        for (int i = 0; i < values.Length; i++)
        {
            var rect = new XRect(x, y, widths[i], rowHeight);
            gfx.DrawRectangle(pen, rect);
            gfx.DrawString(values[i], font, XBrushes.Black, new XRect(x + 4, y + 2, widths[i] - 8, rowHeight), XStringFormats.TopLeft);
            x += widths[i];
        }
    }

    private static double DrawTableRowWrapped(XGraphics gfx, XFont font, double left, double y, double[] widths, string[] values, HashSet<int> wrapColumns)
    {
        var pen = new XPen(XColors.LightGray, 0.3);
        double x = left;
        double lineHeight = 16;
        double requiredHeight = lineHeight;
        for (int i = 0; i < values.Length; i++)
        {
            if (wrapColumns.Contains(i))
            {
                var lines = WrapText(values[i] ?? string.Empty, gfx, font, widths[i] - 8);
                requiredHeight = Math.Max(requiredHeight, Math.Max(lineHeight, lines.Count * lineHeight));
            }
        }
        for (int i = 0; i < values.Length; i++)
        {
            var rect = new XRect(x, y, widths[i], requiredHeight);
            gfx.DrawRectangle(pen, rect);
            if (wrapColumns.Contains(i))
            {
                var lines = WrapText(values[i] ?? string.Empty, gfx, font, widths[i] - 8);
                double yy = y + 2;
                foreach (var line in lines)
                {
                    gfx.DrawString(line, font, XBrushes.Black, new XRect(x + 4, yy, widths[i] - 8, lineHeight), XStringFormats.TopLeft);
                    yy += lineHeight;
                }
            }
            else
            {
                gfx.DrawString(values[i] ?? string.Empty, font, XBrushes.Black, new XRect(x + 4, y + 2, widths[i] - 8, requiredHeight), XStringFormats.TopLeft);
            }
            x += widths[i];
        }
        return requiredHeight;
    }

    private static List<string> WrapText(string text, XGraphics gfx, XFont font, double maxWidth)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) { result.Add(string.Empty); return result; }
        var words = text.Split(' ');
        string current = string.Empty;
        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(current) ? word : current + " " + word;
            var size = gfx.MeasureString(candidate, font);
            if (size.Width <= maxWidth)
            {
                current = candidate;
            }
            else
            {
                if (!string.IsNullOrEmpty(current)) result.Add(current);
                if (gfx.MeasureString(word, font).Width > maxWidth)
                {
                    var pieces = HardWrap(word, gfx, font, maxWidth);
                    if (pieces.Count > 0)
                    {
                        result.AddRange(pieces.Take(pieces.Count - 1));
                        current = pieces.Last();
                    }
                    else
                    {
                        current = word;
                    }
                }
                else
                {
                    current = word;
                }
            }
        }
        if (!string.IsNullOrEmpty(current)) result.Add(current);
        if (result.Count == 0) result.Add(string.Empty);
        return result;
    }

    private static List<string> HardWrap(string word, XGraphics gfx, XFont font, double maxWidth)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(word)) { lines.Add(string.Empty); return lines; }
        int start = 0;
        while (start < word.Length)
        {
            int len = 1;
            while (start + len <= word.Length && gfx.MeasureString(word.Substring(start, len), font).Width <= maxWidth)
            {
                len++;
            }
            len = Math.Max(1, len - 1);
            lines.Add(word.Substring(start, len));
            start += len;
        }
        return lines;
    }

    public async Task<byte[]> ExportAttendanceListAsync(int listId)
    {
        try
        {
            var list = await _attendanceRepo.GetByIdAsync(listId);
            if (list == null) throw new ArgumentException("List not found");

            var entries = await _attendanceEntryRepo.GetByListIdAsync(listId);

            var font = CreateFont("CreatoDisplay", 12);
            var titleFont = CreateFont("CreatoDisplay", 16, true);
            var headerBold = CreateFont("CreatoDisplay", 12, true);

            try
            {
                if (list == null)
                {
                    throw new ArgumentException("List not found");
                }
                
                if (entries == null)
                {
                    throw new ArgumentException("Entries not found");
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            
            var document = new PdfDocument();
            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);

            double left = 40, right = 40, top = 50, bottom = 40;
            double contentWidth = page.Width - left - right;
            int pageNumber = 1;

            DrawHeader(gfx, "ANWESENHEITSLISTE", titleFont, left, top, contentWidth);
            double y = top + 36;
            gfx.DrawString($"Titel: {list.Title}", font, XBrushes.Black, new XRect(left, y, contentWidth, 16), XStringFormats.TopLeft); y += 16;
            gfx.DrawString($"Einheit: {list.Unit}", font, XBrushes.Black, new XRect(left, y, contentWidth, 16), XStringFormats.TopLeft); y += 16;
            gfx.DrawString($"Beschreibung: {list.Description}", font, XBrushes.Black, new XRect(left, y, contentWidth, 16), XStringFormats.TopLeft); y += 16;
            gfx.DrawString($"Erstellt am: {list.CreatedAt:dd.MM.yyyy HH:mm}", font, XBrushes.Black, new XRect(left, y, contentWidth, 16), XStringFormats.TopLeft); y += 16;
            gfx.DrawString($"Status: {(list.Status == ListStatus.Open ? "Offen" : "Geschlossen")}", font, XBrushes.Black, new XRect(left, y, contentWidth, 16), XStringFormats.TopLeft); y += 22;

            var presentEntries = entries.Where(e => !e.IsExcused).ToList();
            var excusedEntries = entries.Where(e => e.IsExcused).ToList();

            gfx.DrawString($"Anwesend ({presentEntries.Count})", headerBold, XBrushes.Black, new XRect(left, y, contentWidth, 16), XStringFormats.TopLeft);
            y += 20;

            var widths = new[] { 40d, contentWidth - 120d, 80d };
            var attendanceHeaderBold = CreateFont("CreatoDisplay", 12, true);
            DrawTableHeader(gfx, font, attendanceHeaderBold, left, y, widths, new[] { "#", "Name/ID", "Uhrzeit" });
            y += 16;

            for (int i = 0; i < presentEntries.Count; i++)
            {
                if (y > page.Height - bottom - 24)
                {
                    DrawFooter(document, page, gfx, font, left, page.Height, contentWidth, pageNumber);
                    page = document.AddPage(); pageNumber++;
                    gfx.Dispose(); gfx = XGraphics.FromPdfPage(page);
                    DrawHeader(gfx, "ANWESENHEITSLISTE", titleFont, left, top, contentWidth);
                    y = top + 36;
                    DrawTableHeader(gfx, font, attendanceHeaderBold, left, y, widths, new[] { "#", "Name/ID", "Uhrzeit" });
                    y += 16;
                }
                var e = presentEntries[i];
                DrawTableRow(gfx, font, left, y, widths, new[] { (i + 1).ToString(), e.NameOrId, e.EnteredAt.ToString("HH:mm:ss") });
                y += 16;
            }

            if (excusedEntries.Count > 0)
            {
                y += 12;
                if (y > page.Height - bottom - 100)
                {
                    DrawFooter(document, page, gfx, font, left, page.Height, contentWidth, pageNumber);
                    page = document.AddPage(); pageNumber++;
                    gfx.Dispose(); gfx = XGraphics.FromPdfPage(page);
                    DrawHeader(gfx, "ANWESENHEITSLISTE", titleFont, left, top, contentWidth);
                    y = top + 36;
                }

                gfx.DrawString($"Entschuldigt ({excusedEntries.Count})", headerBold, XBrushes.Black, new XRect(left, y, contentWidth, 16), XStringFormats.TopLeft);
                y += 20;
                DrawTableHeader(gfx, font, attendanceHeaderBold, left, y, widths, new[] { "#", "Name/ID", "Uhrzeit" });
                y += 16;

                for (int i = 0; i < excusedEntries.Count; i++)
                {
                    if (y > page.Height - bottom - 24)
                    {
                        DrawFooter(document, page, gfx, font, left, page.Height, contentWidth, pageNumber);
                        page = document.AddPage(); pageNumber++;
                        gfx.Dispose(); gfx = XGraphics.FromPdfPage(page);
                        DrawHeader(gfx, "ANWESENHEITSLISTE", titleFont, left, top, contentWidth);
                        y = top + 36;
                        DrawTableHeader(gfx, font, attendanceHeaderBold, left, y, widths, new[] { "#", "Name/ID", "Uhrzeit" });
                        y += 16;
                    }
                    var e = excusedEntries[i];
                    DrawTableRow(gfx, font, left, y, widths, new[] { (i + 1).ToString(), e.NameOrId, e.EnteredAt.ToString("HH:mm:ss") });
                    y += 16;
                }
            }

            DrawFooter(document, page, gfx, font, left, page.Height, contentWidth, pageNumber);

            using var memoryStream = new MemoryStream();
            document.Save(memoryStream);
            var pdfBytes = memoryStream.ToArray();
            return pdfBytes;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Fehler beim PDF-Export: {ex.GetType().Name}: {ex.Message}", ex);
        }
    }

    public async Task<byte[]> ExportOperationListAsync(int listId)
    {
        try
        {
            var list = await _operationRepo.GetByIdAsync(listId);
            if (list == null)
            {
                throw new ArgumentException("List not found");
            }

            var entries = await _operationEntryRepo.GetByListIdAsync(listId);
            if (entries == null)
            {
                entries = new List<OperationEntry>();
            }

            var entryIds = entries.Select(e => e.Id).ToList();
            var functionsByEntry = await _entryFunctionRepo.GetFunctionsForEntriesAsync(entryIds);

            var document = new PdfDocument();
            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);

            var font = CreateFont("CreatoDisplay", 12);
            var titleFont = CreateFont("CreatoDisplay", 16, true);
            var headerBold = CreateFont("CreatoDisplay", 12, true);

            double left = 40, right = 40, top = 50, bottom = 40;
            double contentWidth = page.Width - left - right;
            int pageNumber = 1;

            DrawHeader(gfx, "EINSATZLISTE", titleFont, left, top, contentWidth);
            double yPosition = top + 36;
            gfx.DrawString($"Einsatznummer: {list.OperationNumber ?? string.Empty}", font, XBrushes.Black, new XRect(left, yPosition, contentWidth, 16), XStringFormats.TopLeft); yPosition += 16;
            gfx.DrawString($"Stichwort: {list.Keyword ?? string.Empty}", font, XBrushes.Black, new XRect(left, yPosition, contentWidth, 16), XStringFormats.TopLeft); yPosition += 16;
            gfx.DrawString($"Alarmzeit: {list.AlertTime:dd.MM.yyyy HH:mm}", font, XBrushes.Black, new XRect(left, yPosition, contentWidth, 16), XStringFormats.TopLeft); yPosition += 16;
            gfx.DrawString($"Erstellt am: {list.CreatedAt:dd.MM.yyyy HH:mm}", font, XBrushes.Black, new XRect(left, yPosition, contentWidth, 16), XStringFormats.TopLeft); yPosition += 16;
            gfx.DrawString($"Status: {(list.Status == ListStatus.Open ? "Offen" : "Geschlossen")}", font, XBrushes.Black, new XRect(left, yPosition, contentWidth, 16), XStringFormats.TopLeft); yPosition += 16;
            if (!string.IsNullOrWhiteSpace(list.Address))
            {
                gfx.DrawString($"Adresse: {list.Address}", font, XBrushes.Black, new XRect(left, yPosition, contentWidth - 200, 16), XStringFormats.TopLeft);
            }
            yPosition += 22;

            if (!string.IsNullOrWhiteSpace(list.Address) || (list.Latitude.HasValue && list.Longitude.HasValue))
            {
                double mapW = 200, mapH = 140;
                var mapRect = new XRect(page.Width - 40 - mapW, top + 6, mapW, mapH);
                var mapImage = await TryLoadStaticMapAsync(list.Address, list.Latitude, list.Longitude, (int)mapW, (int)mapH);
                if (mapImage != null)
                {
                    gfx.DrawImage(mapImage, mapRect);
                }
                else
                {
                    var pen = new XPen(XColors.Gray, 0.5);
                    gfx.DrawRectangle(XBrushes.WhiteSmoke, mapRect);
                    gfx.DrawRectangle(pen, mapRect);
                    gfx.DrawString("Kartenansicht", font, XBrushes.Gray, new XRect(mapRect.X + 8, mapRect.Y + 8, mapRect.Width - 16, 16), XStringFormats.TopLeft);
                    gfx.DrawString("(OSM)", font, XBrushes.Gray, new XRect(mapRect.X + 8, mapRect.Y + 24, mapRect.Width - 16, 16), XStringFormats.TopLeft);
                }
            }

            var vehicleGroups = entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Vehicle))
                .GroupBy(e => e.Vehicle!)
                .OrderBy(g => g.Key);

            foreach (var vehicleGroup in vehicleGroups)
            {
                gfx.DrawString($"Fahrzeug: {vehicleGroup.Key} ({vehicleGroup.Count()} Teilnehmer)", titleFont, XBrushes.Black, new XRect(left, yPosition, contentWidth, 18), XStringFormats.TopLeft);
                yPosition += 22;

                var widths = new[] { 40d, contentWidth - 260d, 160d, 60d };
                DrawTableHeader(gfx, font, headerBold, left, yPosition, widths, new[] { "#", "Name/ID", "Funktionen", "UA" });
                yPosition += 16;

                var vehicleEntries = vehicleGroup.ToList();
                for (int i = 0; i < vehicleEntries.Count; i++)
                {
                    if (yPosition > page.Height - bottom - 24)
                    {
                        DrawFooter(document, page, gfx, font, left, page.Height, contentWidth, pageNumber);
                        page = document.AddPage(); pageNumber++;
                        gfx.Dispose(); gfx = XGraphics.FromPdfPage(page);
                        DrawHeader(gfx, "EINSATZLISTE", titleFont, left, top, contentWidth);
                        yPosition = top + 36;
                        gfx.DrawString($"Fahrzeug: {vehicleGroup.Key}", font, XBrushes.Black, new XRect(left, yPosition, contentWidth, 16), XStringFormats.TopLeft); yPosition += 18;
                        DrawTableHeader(gfx, font, headerBold, left, yPosition, widths, new[] { "#", "Name/ID", "Funktionen", "UA" });
                        yPosition += 16;
                    }
                    var entry = vehicleEntries[i];
                    var name = entry.NameOrId ?? string.Empty;
                    var functionText = functionsByEntry.TryGetValue(entry.Id, out var listFns)
                        ? string.Join(", ", listFns.Select(x => x.Name))
                        : entry.Function.ToString();
                    var atem = entry.WithBreathingApparatus ? "Ja" : "Nein";
                    var rowH = DrawTableRowWrapped(gfx, font, left, yPosition, widths, new[] { (i + 1).ToString(), name, functionText, atem }, new HashSet<int> { 2 });
                    yPosition += rowH;
                }
                yPosition += 10;
            }

            var noVehicle = entries.Where(e => string.IsNullOrWhiteSpace(e.Vehicle)).ToList();
            if (noVehicle.Count > 0)
            {
                if (yPosition > page.Height - bottom - 80)
                {
                    DrawFooter(document, page, gfx, font, left, page.Height, contentWidth, pageNumber);
                    page = document.AddPage(); pageNumber++;
                    gfx.Dispose(); gfx = XGraphics.FromPdfPage(page);
                    DrawHeader(gfx, "EINSATZLISTE", titleFont, left, top, contentWidth);
                    yPosition = top + 36;
                }
                gfx.DrawString($"Ohne Fahrzeug ({noVehicle.Count})", titleFont, XBrushes.Black, new XRect(left, yPosition, contentWidth, 18), XStringFormats.TopLeft);
                yPosition += 22;
                var widths2 = new[] { 40d, contentWidth - 260d, 160d, 60d };
                DrawTableHeader(gfx, font, headerBold, left, yPosition, widths2, new[] { "#", "Name/ID", "Funktionen", "UA" });
                yPosition += 16;
                for (int i = 0; i < noVehicle.Count; i++)
                {
                    if (yPosition > page.Height - bottom - 24)
                    {
                        DrawFooter(document, page, gfx, font, left, page.Height, contentWidth, pageNumber);
                        page = document.AddPage(); pageNumber++;
                        gfx.Dispose(); gfx = XGraphics.FromPdfPage(page);
                        DrawHeader(gfx, "EINSATZLISTE", titleFont, left, top, contentWidth);
                        yPosition = top + 36;
                        DrawTableHeader(gfx, font, headerBold, left, yPosition, widths2, new[] { "#", "Name/ID", "Funktionen", "UA" });
                        yPosition += 16;
                    }
                    var entry = noVehicle[i];
                    var name = entry.NameOrId ?? string.Empty;
                    var functionText = functionsByEntry.TryGetValue(entry.Id, out var listFns)
                        ? string.Join(", ", listFns.Select(x => x.Name))
                        : entry.Function.ToString();
                    var atem = entry.WithBreathingApparatus ? "Ja" : "Nein";
                    var rowH = DrawTableRowWrapped(gfx, font, left, yPosition, widths2, new[] { (i + 1).ToString(), name, functionText, atem }, new HashSet<int> { 2 });
                    yPosition += rowH;
                }
            }

            DrawFooter(document, page, gfx, font, left, page.Height, contentWidth, pageNumber);

            using var memoryStream = new MemoryStream();
            document.Save(memoryStream);
            var bytes = memoryStream.ToArray();
            return bytes;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Fehler beim PDF-Export: {ex.GetType().Name}: {ex.Message}", ex);
        }
    }

    private async Task<XImage?> TryLoadStaticMapAsync(string? address, double? latitude, double? longitude, int width, int height)
    {
        const int oversample = 4;
        double? lat = latitude;
        double? lon = longitude;
        if (!lat.HasValue || !lon.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(address))
            {
                var (glat, glon) = await _geocodingService.GeocodeAsync(address);
                lat = glat;
                lon = glon;
            }
        }
        if (!lat.HasValue || !lon.HasValue) return null;

        var latStr = lat.Value.ToString(CultureInfo.InvariantCulture);
        var lonStr = lon.Value.ToString(CultureInfo.InvariantCulture);
        var zoom = 14;
        var urls = new List<string>
        {
            $"https://staticmap.openstreetmap.de/staticmap.php?center={latStr},{lonStr}&zoom={zoom}&size={width*oversample}x{height*oversample}&maptype=mapnik&markers={latStr},{lonStr},red-pushpin",
            $"https://staticmap.openstreetmap.fr/staticmap.php?center={latStr},{lonStr}&zoom={zoom}&size={width*oversample}x{height*oversample}&markers={latStr},{lonStr},red-pushpin"
        };
        foreach (var url in urls)
        {
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("FeuerwehrListen/1.0 (+https://localhost)");
                http.DefaultRequestHeaders.Accept.ParseAdd("image/png");
                var bytes = await http.GetByteArrayAsync(url);
                if (bytes != null && bytes.Length > 0)
                {
                    var ms = new MemoryStream(bytes);
                    return XImage.FromStream(ms);
                }
            }
            catch
            {
            }
        }
        var tileImage = await BuildTileStaticMapAsync(lat.Value, lon.Value, width*oversample, height*oversample);
        if (tileImage != null) return tileImage;
        return null;
    }

    private static (double xtile, double ytile) LatLonToTile(double lat, double lon, int zoom)
    {
        var latRad = lat * Math.PI / 180.0;
        var n = Math.Pow(2.0, zoom);
        var xtile = (lon + 180.0) / 360.0 * n;
        var ytile = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n;
        return (xtile, ytile);
    }

    private async Task<XImage?> BuildTileStaticMapAsync(double lat, double lon, int width, int height)
    {
        const int tileSize = 256;
        const int zoom = 15;
        var (xtileD, ytileD) = LatLonToTile(lat, lon, zoom);
        int xCenter = (int)Math.Floor(xtileD);
        int yCenter = (int)Math.Floor(ytileD);

        int tilesX = 3, tilesY = 3;
        int totalW = tilesX * tileSize;
        int totalH = tilesY * tileSize;

        using var canvas = new Bitmap(totalW, totalH);
        using (var g = Graphics.FromImage(canvas))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.White);

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int xt = xCenter + dx;
                    int yt = yCenter + dy;
                    var tileBytes = await TryFetchTileAsync(zoom, xt, yt);
                    if (tileBytes != null)
                    {
                        using var ms = new MemoryStream(tileBytes);
                        using var tile = Image.FromStream(ms);
                        g.DrawImage(tile, (dx + 1) * tileSize, (dy + 1) * tileSize, tileSize, tileSize);
                    }
                    else
                    {
                        using var placeholder = new Bitmap(tileSize, tileSize);
                        using var pg = Graphics.FromImage(placeholder);
                        pg.Clear(Color.LightGray);
                        pg.DrawRectangle(Pens.Gray, 0, 0, tileSize - 1, tileSize - 1);
                        g.DrawImage(placeholder, (dx + 1) * tileSize, (dy + 1) * tileSize);
                    }
                }
            }

            double fracX = (xtileD - Math.Floor(xtileD));
            double fracY = (ytileD - Math.Floor(ytileD));
            float centerX = (float)((1 + fracX) * tileSize);
            float centerY = (float)((1 + fracY) * tileSize);
            float r = 10f;
            using var brush = new SolidBrush(Color.Red);
            using var whitePen = new Pen(Color.White, 3f);
            using var blackPen = new Pen(Color.Black, 1f);
            g.FillEllipse(brush, centerX - r, centerY - r, r * 2, r * 2);
            g.DrawEllipse(whitePen, centerX - r, centerY - r, r * 2, r * 2);
            g.DrawEllipse(blackPen, centerX - r, centerY - r, r * 2, r * 2);
        }

        using var output = new Bitmap(width, height);
        using (var og = Graphics.FromImage(output))
        {
            og.SmoothingMode = SmoothingMode.AntiAlias;
            og.CompositingQuality = CompositingQuality.HighQuality;
            og.PixelOffsetMode = PixelOffsetMode.HighQuality;
            og.InterpolationMode = InterpolationMode.HighQualityBicubic;
            og.Clear(Color.White);
            double targetAspect = (double)width / height;
            double srcAspect = (double)canvas.Width / canvas.Height;
            Rectangle srcRect;
            if (targetAspect > srcAspect)
            {
                int newH = (int)Math.Round(canvas.Width / targetAspect);
                int y0 = Math.Max(0, (canvas.Height - newH) / 2);
                srcRect = new Rectangle(0, y0, canvas.Width, Math.Min(newH, canvas.Height));
            }
            else
            {
                int newW = (int)Math.Round(canvas.Height * targetAspect);
                int x0 = Math.Max(0, (canvas.Width - newW) / 2);
                srcRect = new Rectangle(x0, 0, Math.Min(newW, canvas.Width), canvas.Height);
            }
            og.DrawImage(canvas, new Rectangle(0, 0, width, height), srcRect, GraphicsUnit.Pixel);
        }
        ApplyLightSharpen(output);
        var outMs = new MemoryStream();
        output.Save(outMs, System.Drawing.Imaging.ImageFormat.Png);
        outMs.Position = 0;
        return XImage.FromStream(outMs);
    }

    private static void ApplyLightSharpen(Bitmap bitmap)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;
        using var src = (Bitmap)bitmap.Clone();
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                var c = src.GetPixel(x, y);
                int r = 5 * c.R;
                int g = 5 * c.G;
                int b = 5 * c.B;
                var c1 = src.GetPixel(x - 1, y);
                var c2 = src.GetPixel(x + 1, y);
                var c3 = src.GetPixel(x, y - 1);
                var c4 = src.GetPixel(x, y + 1);
                r -= c1.R + c2.R + c3.R + c4.R;
                g -= c1.G + c2.G + c3.G + c4.G;
                b -= c1.B + c2.B + c3.B + c4.B;
                r = Math.Clamp(r, 0, 255);
                g = Math.Clamp(g, 0, 255);
                b = Math.Clamp(b, 0, 255);
                bitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
            }
        }
    }

    private static async Task<byte[]?> TryFetchTileAsync(int z, int x, int y)
    {
        var urls = new[]
        {
            $"https://tile.openstreetmap.org/{z}/{x}/{y}.png",
            $"https://a.tile.openstreetmap.org/{z}/{x}/{y}.png",
            $"https://b.tile.openstreetmap.org/{z}/{x}/{y}.png",
            $"https://c.tile.openstreetmap.org/{z}/{x}/{y}.png"
        };
        foreach (var url in urls)
        {
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("FeuerwehrListen/1.0 (+https://localhost)");
                http.DefaultRequestHeaders.Accept.ParseAdd("image/png");
                var bytes = await http.GetByteArrayAsync(url);
                if (bytes != null && bytes.Length > 0) return bytes;
            }
            catch
            {
            }
        }
        return null;
    }

    public async Task<byte[]> ExportStatisticsReportAsync()
    {
        try
        {
            var document = new PdfDocument();
            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);

            var font = CreateFont("CreatoDisplay", 12);
            var titleFont = CreateFont("CreatoDisplay", 16, true);
            var boldFont = CreateFont("CreatoDisplay", 12, true);
            double left = 40, right = 40, top = 50, bottom = 40;
            double contentWidth = page.Width - left - right;
            int pageNumber = 1;

            DrawHeader(gfx, "STATISTIK-BERICHT", titleFont, left, top, contentWidth);
            double yPosition = top + 36;
            gfx.DrawString($"Bericht erstellt am: {DateTime.Now:dd.MM.yyyy HH:mm}", font, XBrushes.Black, new XRect(left, yPosition, contentWidth, 16), XStringFormats.TopLeft);
            yPosition += 22;

            var overview = await _statisticsService.GetListStatisticsAsync();
            var topParticipants = await _statisticsService.GetTopParticipantsAsync(10);
            var vehicleStats = await _statisticsService.GetVehicleStatisticsAsync();
            var functionStats = await _statisticsService.GetFunctionStatisticsAsync();
            var breathingStats = await _statisticsService.GetBreathingApparatusStatisticsAsync();

            var headerBold = CreateFont("CreatoDisplay", 12, true);
            DrawTableHeader(gfx, font, headerBold, left, yPosition, new[] { contentWidth / 2, contentWidth / 2 }, new[] { "Kennzahl", "Wert" });
            yPosition += 16;
            DrawTableRow(gfx, font, left, yPosition, new[] { contentWidth / 2, contentWidth / 2 }, new[] { "Gesamt Listen", overview.TotalLists.ToString() }); yPosition += 18;
            DrawTableRow(gfx, font, left, yPosition, new[] { contentWidth / 2, contentWidth / 2 }, new[] { "Offen | Geschlossen | Archiviert", $"{overview.OpenLists} | {overview.ClosedLists} | {overview.ArchivedLists}" }); yPosition += 18;
            DrawTableRow(gfx, font, left, yPosition, new[] { contentWidth / 2, contentWidth / 2 }, new[] { "Ø Teilnehmer pro Liste", overview.AverageParticipants.ToString() }); yPosition += 18;
            DrawTableRow(gfx, font, left, yPosition, new[] { contentWidth / 2, contentWidth / 2 }, new[] { "Gesamt Teilnahmen", overview.TotalParticipants.ToString() }); yPosition += 24;

            yPosition += 6;
            PaginateIfNeeded(document, ref page, ref gfx, ref yPosition);
            gfx.DrawString("Top Teilnehmer (Top 10)", titleFont, XBrushes.Black, new XRect(left, yPosition, contentWidth, 18), XStringFormats.TopLeft);
            yPosition += 22;
            DrawTableHeader(gfx, font, headerBold, left, yPosition, new[] { 40d, 220d, contentWidth - 40d - 220d - 90d, 90d }, new[] { "#", "Name", "Teilnahmen", "Anteil" });
            yPosition += 16;
            int rank = 1;
            foreach (var p in topParticipants)
            {
                PaginateIfNeeded(document, ref page, ref gfx, ref yPosition);
                DrawTableRow(gfx, font, left, yPosition, new[] { 40d, 220d, contentWidth - 40d - 220d - 90d, 90d }, new[] { rank.ToString(), $"{p.MemberName} ({p.MemberNumber})", p.ParticipationCount.ToString(), $"{p.Percentage:F1}%" });
                yPosition += 16; rank++;
            }
            yPosition += 18;
            yPosition += 6;

            PaginateIfNeeded(document, ref page, ref gfx, ref yPosition);
            gfx.DrawString("Fahrzeug-Nutzung", titleFont, XBrushes.Black, new XRect(left, yPosition, contentWidth, 18), XStringFormats.TopLeft);
            yPosition += 22;
            var vehicleWidths = new[] { contentWidth * 0.4, contentWidth * 0.2, contentWidth * 0.2, contentWidth * 0.2 };
            DrawTableHeader(gfx, font, headerBold, left, yPosition, vehicleWidths, new[] { "Fahrzeug", "Einsätze", "Anteil", "Ø Besatzung" });
            yPosition += 16;
            foreach (var vehicle in vehicleStats.OrderByDescending(v => v.UsageCount))
            {
                PaginateIfNeeded(document, ref page, ref gfx, ref yPosition);
                DrawTableRow(gfx, font, left, yPosition, vehicleWidths, new[]
                {
                    vehicle.VehicleName,
                    vehicle.UsageCount.ToString(),
                    $"{vehicle.UsagePercentage:F1}%",
                    vehicle.AverageCrew.ToString("F1")
                });
                yPosition += 16;
            }
            yPosition += 18;
            yPosition += 6;

            PaginateIfNeeded(document, ref page, ref gfx, ref yPosition);
            gfx.DrawString("Funktionen-Verteilung", titleFont, XBrushes.Black, new XRect(left, yPosition, contentWidth, 18), XStringFormats.TopLeft);
            yPosition += 22;
            DrawTableHeader(gfx, font, headerBold, left, yPosition, new[] { contentWidth - 120d, 60d, 60d }, new[] { "Funktion", "Anzahl", "Anteil" });
            yPosition += 16;
            foreach (var f in functionStats)
            {
                PaginateIfNeeded(document, ref page, ref gfx, ref yPosition);
                DrawTableRow(gfx, font, left, yPosition, new[] { contentWidth - 120d, 60d, 60d }, new[] { f.FunctionName, f.Count.ToString(), $"{f.Percentage:F1}%" });
                yPosition += 16;
            }
            yPosition += 18;
            yPosition += 6;

            PaginateIfNeeded(document, ref page, ref gfx, ref yPosition);
            gfx.DrawString("Unter Atemschutz", titleFont, XBrushes.Black, new XRect(left, yPosition, contentWidth, 18), XStringFormats.TopLeft);
            yPosition += 22;
            DrawTableHeader(gfx, font, headerBold, left, yPosition, new[] { contentWidth - 140d, 140d }, new[] { "Kategorie", "Wert" });
            yPosition += 16;
            DrawTableRow(gfx, font, left, yPosition, new[] { contentWidth - 140d, 140d }, new[] { "Unter Atemschutz", breathingStats.WithApparatus.ToString() }); yPosition += 16;
            DrawTableRow(gfx, font, left, yPosition, new[] { contentWidth - 140d, 140d }, new[] { "Ohne Atemschutz", breathingStats.WithoutApparatus.ToString() }); yPosition += 16;
            DrawTableRow(gfx, font, left, yPosition, new[] { contentWidth - 140d, 140d }, new[] { "Anteil unter Atemschutz", $"{breathingStats.WithApparatusPercentage:F1}%" });

            DrawFooter(document, page, gfx, font, left, page.Height, contentWidth, pageNumber);
            using var memoryStream = new MemoryStream();
            document.Save(memoryStream);
            return memoryStream.ToArray();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Fehler beim PDF-Export: {ex.GetType().Name}: {ex.Message}", ex);
        }
    }

    private static void PaginateIfNeeded(PdfDocument document, ref PdfPage page, ref XGraphics gfx, ref double y)
    {
        if (y > page.Height - 50)
        {
            page = document.AddPage();
            gfx.Dispose();
            gfx = XGraphics.FromPdfPage(page);
            y = 50;
        }
    }
}