// ============================================================
// Feuerwehr Tag Helper - lokaler Render-Dienst (localhost)
// ============================================================
// Rendert Feuerwehr-Tags (body.stl + inlay.stl) mit dem LOKAL installierten OpenSCAD
// auf dem PC des Admins. Die FeuerwehrListen-Web-App (im Browser) ruft diesen Helfer
// unter http://localhost:47800 auf - so bleibt die kleine Server-VM komplett unbelastet.
//
// Starten:  fwtag-helper.exe            (Port 47800)
//           fwtag-helper.exe 47900      (anderer Port)
// OpenSCAD wird automatisch gesucht (Program Files / PATH); im Browser ueberschreibbar.
// ============================================================

using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

var port = 47800;
if (args.Length > 0 && int.TryParse(args[0], out var p)) port = p;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args });
builder.Logging.ClearProviders();
builder.WebHost.UseUrls($"http://localhost:{port}");
var app = builder.Build();

// CORS: der Aufruf kommt cross-origin von der (HTTPS-)Web-App. Loopback ist von der
// Mixed-Content-Sperre ausgenommen; wir erlauben jeden Origin (nur localhost gebunden).
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["Access-Control-Allow-Origin"] = "*";
    ctx.Response.Headers["Access-Control-Allow-Methods"] = "GET, OPTIONS";
    ctx.Response.Headers["Access-Control-Allow-Headers"] = "*";
    if (HttpMethods.IsOptions(ctx.Request.Method)) { ctx.Response.StatusCode = 204; return; }
    await next();
});

var scadDir = Path.Combine(AppContext.BaseDirectory, "scad");
var defaultScad = Path.Combine(scadDir, "feuerwehr_tag.scad");

// --- Status/Erreichbarkeit ---
app.MapGet("/ping", () =>
{
    var oscad = ResolveOpenScad(null);
    return Results.Json(new
    {
        ok = true,
        app = "fwtag-helper",
        openscad = oscad,
        openscadFound = oscad != null && File.Exists(oscad),
        scad = File.Exists(defaultScad) ? defaultScad : null,
    });
});

// --- Tag rendern: body.stl + inlay.stl als ZIP ---
app.MapGet("/tag", async (string? number, string? name, string? openscad, string? scad) =>
{
    number = (number ?? "").Trim();
    name = (name ?? "").Trim();
    if (string.IsNullOrEmpty(number))
        return Results.BadRequest("Parameter 'number' fehlt.");

    var oscad = ResolveOpenScad(openscad);
    if (oscad == null || !File.Exists(oscad))
        return Results.Problem($"OpenSCAD nicht gefunden. Bitte im Browser den Pfad zu openscad.com/.exe setzen. (Versucht: {oscad ?? "-"})", statusCode: 400);

    var scadFile = string.IsNullOrWhiteSpace(scad) ? defaultScad : scad.Trim();
    if (!File.Exists(scadFile))
        return Results.Problem($"SCAD-Datei nicht gefunden: {scadFile}", statusCode: 400);

    var work = Path.Combine(Path.GetTempPath(), "fwtag_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(work);
    try
    {
        var bodyFile = Path.Combine(work, $"feuerwehr_tag_{Safe(number)}_body.stl");
        var inlayFile = Path.Combine(work, $"feuerwehr_tag_{Safe(number)}_inlay.stl");

        // Name/Nummer NICHT ueber die Kommandozeile (-D) uebergeben: Umlaute brechen dort auf
        // Windows (falsche Codepage -> Kasten statt ü). Stattdessen ueber eine ASCII-JSON-
        // Parameterdatei (System.Text.Json escaped Umlaute als ü), die OpenSCAD als UTF-8 liest.
        var paramFile = Path.Combine(work, "params.json");
        await File.WriteAllTextAsync(paramFile, BuildParamsJson(name, number));

        var bodyTask = RunOpenScad(oscad, scadFile, "body", bodyFile, paramFile);
        var inlayTask = RunOpenScad(oscad, scadFile, "inlay", inlayFile, paramFile);
        await Task.WhenAll(bodyTask, inlayTask);

        var bodyErr = await bodyTask; var inlayErr = await inlayTask;
        if (bodyErr != null) return Results.Problem($"OpenSCAD (body): {bodyErr}", statusCode: 500);
        if (inlayErr != null) return Results.Problem($"OpenSCAD (inlay): {inlayErr}", statusCode: 500);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddToZip(zip, $"feuerwehr_tag_{Safe(number)}_body.stl", bodyFile);
            AddToZip(zip, $"feuerwehr_tag_{Safe(number)}_inlay.stl", inlayFile);
        }
        return Results.File(ms.ToArray(), "application/zip", $"feuerwehr_tag_{Safe(number)}.zip");
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: 500);
    }
    finally
    {
        try { Directory.Delete(work, recursive: true); } catch { }
    }
});

Console.WriteLine("============================================================");
Console.WriteLine(" Feuerwehr Tag Helper laeuft");
Console.WriteLine($"   URL:      http://localhost:{port}");
Console.WriteLine($"   OpenSCAD: {ResolveOpenScad(null) ?? "NICHT gefunden - im Browser Pfad setzen"}");
Console.WriteLine($"   SCAD:     {(File.Exists(defaultScad) ? defaultScad : "(gebuendelte Vorlage fehlt)")}");
Console.WriteLine(" Fenster offen lassen, solange Tags erzeugt werden. Beenden: Strg+C");
Console.WriteLine("============================================================");

app.Run();
return;

// ============================================================
// Helpers
// ============================================================
static string? ResolveOpenScad(string? preferred)
{
    if (!string.IsNullOrWhiteSpace(preferred))
        return preferred.Trim();

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        string[] candidates =
        {
            @"C:\Program Files\OpenSCAD\openscad.com",
            @"C:\Program Files\OpenSCAD\openscad.exe",
            @"C:\Program Files\OpenSCAD (Nightly)\openscad.com",
            @"C:\Program Files\OpenSCAD (Nightly)\openscad.exe",
            @"C:\Program Files (x86)\OpenSCAD\openscad.exe",
        };
        foreach (var c in candidates)
            if (File.Exists(c)) return c;
        return "openscad"; // Fallback: PATH
    }

    return "openscad"; // Linux/macOS: PATH
}

static async Task<string?> RunOpenScad(string openscad, string scadFile, string mode, string outFile, string paramFile)
{
    var psi = new ProcessStartInfo
    {
        FileName = openscad,
        WorkingDirectory = Path.GetDirectoryName(scadFile)!,
        RedirectStandardError = true,
        RedirectStandardOutput = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };
    psi.ArgumentList.Add("-o");
    psi.ArgumentList.Add(outFile);
    // Text-Parameter kommen aus der UTF-8-JSON (Umlaut-sicher); nur render_mode via -D (ASCII).
    psi.ArgumentList.Add("-p"); psi.ArgumentList.Add(paramFile);
    psi.ArgumentList.Add("-P"); psi.ArgumentList.Add("fw");
    psi.ArgumentList.Add("-D"); psi.ArgumentList.Add($"render_mode=\"{mode}\"");
    psi.ArgumentList.Add(scadFile);

    using var proc = new Process { StartInfo = psi };
    var err = new StringBuilder();
    proc.ErrorDataReceived += (_, e) => { if (e.Data != null) err.AppendLine(e.Data); };
    proc.OutputDataReceived += (_, _) => { };
    try { proc.Start(); }
    catch (Exception ex) { return $"Start fehlgeschlagen ('{openscad}'): {ex.Message}"; }
    proc.BeginErrorReadLine();
    proc.BeginOutputReadLine();

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180));
    try { await proc.WaitForExitAsync(cts.Token); }
    catch (OperationCanceledException) { try { proc.Kill(true); } catch { } return "Timeout (>180s)."; }

    if (proc.ExitCode != 0)
        return $"ExitCode {proc.ExitCode}. {err.ToString().Trim()}";
    if (!File.Exists(outFile) || new FileInfo(outFile).Length == 0)
        return "Ausgabedatei leer/fehlt.";
    return null;
}

static void AddToZip(ZipArchive zip, string entryName, string sourceFile)
{
    var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
    using var es = entry.Open();
    using var fs = File.OpenRead(sourceFile);
    fs.CopyTo(es);
}

// OpenSCAD-Customizer-Parameterdatei (Set "fw"). System.Text.Json escaped Nicht-ASCII
// (ü -> ü) -> reine ASCII-Datei; OpenSCAD dekodiert sie als UTF-8. Loest das Umlaut-Problem.
static string BuildParamsJson(string name, string number)
{
    var set = new Dictionary<string, string>
    {
        ["name_text"] = name ?? string.Empty,
        ["number_text"] = number ?? string.Empty,
        ["qr_content"] = number ?? string.Empty,
    };
    var doc = new Dictionary<string, object>
    {
        ["fileFormatVersion"] = "1",
        ["parameterSets"] = new Dictionary<string, object> { ["fw"] = set },
    };
    return System.Text.Json.JsonSerializer.Serialize(doc);
}

static string Safe(string s)
{
    var sb = new StringBuilder();
    foreach (var ch in s ?? string.Empty)
        sb.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_');
    return sb.Length == 0 ? "tag" : sb.ToString();
}
