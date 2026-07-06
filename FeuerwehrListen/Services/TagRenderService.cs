using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using FeuerwehrListen.Models;
using QRCoder;

namespace FeuerwehrListen.Services;

/// <summary>
/// Erzeugt aus den vendorten SCAD-Vorlagen (Scad/feuerwehr_tag.scad + qr.scad) je Mitglied
/// die 3D-Druck-Dateien (Body + Inlay STL) via OpenSCAD-CLI sowie ein 2D-Vorschau-SVG.
///
/// - STL: OpenSCAD rendert headless (CGAL, kein Display/xvfb noetig). Body und Inlay laufen
///   parallel. Im Docker-Image ist "openscad" + fonts-liberation installiert.
/// - Vorschau: pur in C# gezeichnetes SVG (blaue Tag-Flaeche + rote Texte/QR an den exakten
///   SCAD-Positionen) - robust, ohne OpenGL. Body = Blau, Inlay = Rot.
/// </summary>
public class TagRenderService
{
    private readonly IConfiguration _config;
    private readonly ILogger<TagRenderService> _logger;

    public TagRenderService(IConfiguration config, ILogger<TagRenderService> logger)
    {
        _config = config;
        _logger = logger;
    }

    // OpenSCAD-Binary: im Docker-Image "openscad" (PATH); lokal via appsettings/ENV ueberschreibbar.
    private string OpenScadPath => _config["OpenScad:Path"]
        ?? Environment.GetEnvironmentVariable("OPENSCAD_PATH")
        ?? "openscad";

    private static string ScadFile => Path.Combine(AppContext.BaseDirectory, "Scad", "feuerwehr_tag.scad");

    // Name-Format wie in export_all.sh: "V. Nachname"
    public static string DisplayName(Member m)
    {
        var initial = string.IsNullOrWhiteSpace(m.FirstName) ? "" : m.FirstName.Trim()[..1] + ". ";
        return (initial + (m.LastName ?? string.Empty).Trim()).Trim();
    }

    // ---- Tag-Geometrie (muss mit feuerwehr_tag.scad uebereinstimmen) ----
    private const double TagWidth = 40, TagHeight = 60, CornerR = 5;
    private const double SlotWidth = 15, SlotHeight = 3.5, SlotCornerR = 1.75, SlotYOffset = 4.5;
    private const double QrSize = 27;
    // Layout-Y (aus der Mitte, positiv = oben)
    private const double Line1Y = 20, Line2Y = 15, NameY = 8, NumberY = 3, QrY = -14;
    private const double SizeLine = 3.8, SizeName = 3.5, SizeNumber = 5.0;
    private const string Line1 = "FEUERWEHR", Line2 = "BILLERBECK";

    /// <summary>ZIP mit body.stl + inlay.stl fuer ein Mitglied (via OpenSCAD).</summary>
    public async Task<byte[]> RenderTagZipAsync(Member member, CancellationToken ct = default)
    {
        var name = DisplayName(member);
        var number = (member.MemberNumber ?? string.Empty).Trim();

        var work = Path.Combine(Path.GetTempPath(), "fwtag_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var bodyFile = Path.Combine(work, $"feuerwehr_tag_{number}_body.stl");
            var inlayFile = Path.Combine(work, $"feuerwehr_tag_{number}_inlay.stl");

            // Body + Inlay parallel rendern (je ~20s CGAL).
            var bodyTask = RunOpenScadAsync("body", bodyFile, name, number, ct);
            var inlayTask = RunOpenScadAsync("inlay", inlayFile, name, number, ct);
            await Task.WhenAll(bodyTask, inlayTask);

            if (!File.Exists(bodyFile) || new FileInfo(bodyFile).Length == 0)
                throw new InvalidOperationException("Body-STL wurde nicht erzeugt.");
            if (!File.Exists(inlayFile) || new FileInfo(inlayFile).Length == 0)
                throw new InvalidOperationException("Inlay-STL wurde nicht erzeugt.");

            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                await AddToZipAsync(zip, $"feuerwehr_tag_{number}_body.stl", bodyFile, ct);
                await AddToZipAsync(zip, $"feuerwehr_tag_{number}_inlay.stl", inlayFile, ct);
            }
            return ms.ToArray();
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch { /* best effort */ }
        }
    }

    private static async Task AddToZipAsync(ZipArchive zip, string entryName, string sourceFile, CancellationToken ct)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var es = entry.Open();
        await using var fs = File.OpenRead(sourceFile);
        await fs.CopyToAsync(es, ct);
    }

    private async Task RunOpenScadAsync(string mode, string outFile, string name, string number, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = OpenScadPath,
            WorkingDirectory = Path.GetDirectoryName(ScadFile)!,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(outFile);
        psi.ArgumentList.Add("-D"); psi.ArgumentList.Add($"render_mode=\"{mode}\"");
        psi.ArgumentList.Add("-D"); psi.ArgumentList.Add($"name_text=\"{Esc(name)}\"");
        psi.ArgumentList.Add("-D"); psi.ArgumentList.Add($"number_text=\"{Esc(number)}\"");
        psi.ArgumentList.Add("-D"); psi.ArgumentList.Add($"qr_content=\"{Esc(number)}\"");
        // Dubai ist auf Linux nicht verfuegbar -> deterministisch auf Liberation Sans Bold setzen.
        psi.ArgumentList.Add("-D"); psi.ArgumentList.Add("font_normal=\"Liberation Sans:style=Bold\"");
        psi.ArgumentList.Add(ScadFile);

        using var proc = new Process { StartInfo = psi };
        var stderr = new StringBuilder();
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
        proc.OutputDataReceived += (_, _) => { };

        try { proc.Start(); }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"OpenSCAD konnte nicht gestartet werden ('{OpenScadPath}'). Ist OpenSCAD installiert? {ex.Message}", ex);
        }
        proc.BeginErrorReadLine();
        proc.BeginOutputReadLine();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(120));
        try
        {
            await proc.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            throw new InvalidOperationException($"OpenSCAD-Timeout beim Rendern ({mode}).");
        }

        if (proc.ExitCode != 0)
        {
            _logger.LogError("OpenSCAD ({Mode}) ExitCode {Code}: {Err}", mode, proc.ExitCode, stderr.ToString());
            throw new InvalidOperationException($"OpenSCAD-Fehler ({mode}, Code {proc.ExitCode}).");
        }
    }

    // Fuer OpenSCAD-String-Literale: Backslash und Anfuehrungszeichen escapen.
    private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");

    // ============================================================
    // Vorschau-SVG (Draufsicht, Body blau + Inlay rot an SCAD-Positionen)
    // ============================================================
    public string BuildPreviewSvg(Member member)
    {
        var name = DisplayName(member);
        var number = (member.MemberNumber ?? string.Empty).Trim();

        const string blue = "#1657b0"; // Body
        const string red = "#e4002b";  // Inlay

        // SCAD (Mitte-Ursprung, +y oben) -> SVG (oben-links, +y unten)
        static double X(double x) => TagWidth / 2 + x;
        static double Y(double y) => TagHeight / 2 - y;

        var sb = new StringBuilder();
        var slotX = X(0) - SlotWidth / 2;
        var slotY = Y(TagHeight / 2 - SlotYOffset) - SlotHeight / 2;

        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"-4 -4 {TagWidth + 8} {TagHeight + 8}\" font-family=\"Arial, Liberation Sans, sans-serif\">");
        // Maske: Tag-Flaeche minus Schluesselring-Schlitz
        sb.Append("<defs><mask id=\"tagmask\">");
        sb.Append($"<rect x=\"0\" y=\"0\" width=\"{TagWidth}\" height=\"{TagHeight}\" rx=\"{CornerR}\" fill=\"white\"/>");
        sb.Append($"<rect x=\"{F(slotX)}\" y=\"{F(slotY)}\" width=\"{SlotWidth}\" height=\"{SlotHeight}\" rx=\"{SlotCornerR}\" fill=\"black\"/>");
        sb.Append("</mask></defs>");
        // Body (blau)
        sb.Append($"<rect x=\"0\" y=\"0\" width=\"{TagWidth}\" height=\"{TagHeight}\" rx=\"{CornerR}\" fill=\"{blue}\" mask=\"url(#tagmask)\"/>");

        // Rote Texte (Inlay)
        AppendText(sb, Line1, X(0), Y(Line1Y), SizeLine, red, bold: true);
        AppendText(sb, Line2, X(0), Y(Line2Y), SizeLine, red, bold: true);
        AppendText(sb, name, X(0), Y(NameY), SizeName, red, bold: true);
        AppendText(sb, number, X(0), Y(NumberY), SizeNumber, red, bold: true);

        // QR (Inlay, rot) - echter QR mit gleichem Inhalt wie im STL
        AppendQr(sb, number, X(0), Y(QrY), QrSize, red);

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static void AppendText(StringBuilder sb, string text, double cx, double cy, double size, string fill, bool bold)
    {
        var weight = bold ? "bold" : "normal";
        sb.Append($"<text x=\"{F(cx)}\" y=\"{F(cy)}\" font-size=\"{F(size)}\" font-weight=\"{weight}\" fill=\"{fill}\" " +
                  $"text-anchor=\"middle\" dominant-baseline=\"central\">{Xml(text)}</text>");
    }

    private static void AppendQr(StringBuilder sb, string content, double cx, double cy, double size, string fill)
    {
        var data = new QRCodeGenerator().CreateQrCode(
            string.IsNullOrWhiteSpace(content) ? " " : content, QRCodeGenerator.ECCLevel.M);
        var matrix = data.ModuleMatrix;
        int n = matrix.Count;
        if (n == 0) return;
        double module = size / n;
        double x0 = cx - size / 2, y0 = cy - size / 2;
        sb.Append("<g>");
        for (int r = 0; r < n; r++)
        {
            var row = matrix[r];
            for (int c = 0; c < n; c++)
            {
                if (!row[c]) continue;
                sb.Append($"<rect x=\"{F(x0 + c * module)}\" y=\"{F(y0 + r * module)}\" width=\"{F(module + 0.02)}\" height=\"{F(module + 0.02)}\" fill=\"{fill}\"/>");
            }
        }
        sb.Append("</g>");
    }

    private static string F(double d) => d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    private static string Xml(string s) => (s ?? string.Empty)
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
