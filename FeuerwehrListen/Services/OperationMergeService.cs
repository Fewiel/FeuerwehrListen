using System.Reflection;
using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;
using LinqToDB;

namespace FeuerwehrListen.Services;

/// <summary>
/// Zwei Einsatzlisten zusammenlegen - typischer Fall: die Leitstelle alarmiert doppelt und
/// es entstehen zwei Listen fuer denselben Einsatz. Der ZIEL-Einsatz bleibt bestehen (Nummer,
/// Stichwort, Zeiten), der QUELL-Einsatz geht darin auf und wird geloescht:
/// * Eintraege wandern mit (Fahrzeug, Funktionen, Atemschutz); wer in beiden Listen steht,
///   bleibt nur einmal (Eintrag des Ziels gewinnt).
/// * Bericht: fehlt er im Ziel, wird der Quell-Bericht uebernommen; sonst werden leere Felder
///   des Ziels aus der Quelle gefuellt, Fremdkraefte/Mittel/Fahrzeugstaerken angehaengt.
/// * Bilder werden in Nextcloud in den Zielordner verschoben (best effort).
/// * Adresse/Koordinaten werden uebernommen, wenn das Ziel keine hat; ist eine Liste offen,
///   bleibt das Ziel offen.
/// </summary>
public class OperationMergeService
{
    private readonly AppDbConnection _db;
    private readonly OperationListRepository _lists;
    private readonly OperationReportRepository _reports;
    private readonly NextcloudService _nc;
    private readonly ILogger<OperationMergeService> _logger;

    public OperationMergeService(AppDbConnection db, OperationListRepository lists, OperationReportRepository reports,
        NextcloudService nc, ILogger<OperationMergeService> logger)
    {
        _db = db;
        _lists = lists;
        _reports = reports;
        _nc = nc;
        _logger = logger;
    }

    public sealed record MergeResult(string? Error, int MovedEntries = 0, int SkippedDuplicates = 0, int ImagesMoved = 0,
        List<string>? ImageErrors = null, string? SourceNumber = null);

    public async Task<MergeResult> MergeAsync(int targetId, int sourceId)
    {
        if (targetId == sourceId) return new MergeResult("Ein Einsatz kann nicht mit sich selbst zusammengelegt werden.");
        var target = await _lists.GetByIdAsync(targetId);
        var source = await _lists.GetByIdAsync(sourceId);
        if (target == null || source == null) return new MergeResult("Einsatz nicht gefunden.");

        // --- Eintraege ---
        var targetEntries = await _db.OperationEntries.Where(e => e.OperationListId == targetId).ToListAsync();
        var sourceEntries = await _db.OperationEntries.Where(e => e.OperationListId == sourceId).ToListAsync();
        var present = targetEntries.Select(e => EntryKey(e.NameOrId)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int moved = 0, skipped = 0;
        foreach (var e in sourceEntries)
        {
            if (present.Contains(EntryKey(e.NameOrId)))
            {
                await _db.OperationEntryFunctions.Where(f => f.OperationEntryId == e.Id).DeleteAsync();
                await _db.OperationEntries.Where(x => x.Id == e.Id).DeleteAsync();
                skipped++;
                continue;
            }
            await _db.OperationEntries.Where(x => x.Id == e.Id).Set(x => x.OperationListId, targetId).UpdateAsync();
            present.Add(EntryKey(e.NameOrId));
            moved++;
        }

        // --- Bericht ---
        var sourceReport = await _reports.GetByOperationListIdAsync(sourceId);
        var targetReport = await _reports.GetByOperationListIdAsync(targetId);
        if (sourceReport != null && targetReport == null)
        {
            await _db.OperationReports.Where(r => r.Id == sourceReport.Id).Set(r => r.OperationListId, targetId).UpdateAsync();
            targetReport = await _reports.GetByOperationListIdAsync(targetId);
        }
        else if (sourceReport != null && targetReport != null)
        {
            FillEmptyFields(targetReport, sourceReport);
            await _db.OperationReportExternalForces.Where(x => x.OperationReportId == sourceReport.Id).Set(x => x.OperationReportId, targetReport.Id).UpdateAsync();
            await _db.OperationReportVehicleStrengths.Where(x => x.OperationReportId == sourceReport.Id).Set(x => x.OperationReportId, targetReport.Id).UpdateAsync();
            await _db.OperationReportMittels.Where(x => x.OperationReportId == sourceReport.Id).Set(x => x.OperationReportId, targetReport.Id).UpdateAsync();
            await _db.OperationReports.Where(r => r.Id == sourceReport.Id).DeleteAsync();
        }
        // Hinweis auf die Zusammenlegung im Lagebericht festhalten - die zweite Einsatznummer
        // der Leitstelle soll nachvollziehbar bleiben.
        targetReport ??= await _reports.GetOrCreateAsync(targetId);
        var note = $"Zusammengelegt mit Einsatz {source.OperationNumber} ({source.AlertTime:dd.MM.yyyy HH:mm}, {source.Keyword}).";
        targetReport.Lagebericht = string.IsNullOrWhiteSpace(targetReport.Lagebericht) ? note : targetReport.Lagebericht.TrimEnd() + "\n" + note;
        targetReport.UpdatedAt = DateTime.Now;
        await _reports.UpdateAsync(targetReport);

        // --- Kopfdaten ---
        if (string.IsNullOrWhiteSpace(target.Address) && !string.IsNullOrWhiteSpace(source.Address))
        {
            target.Address = source.Address; target.Latitude = source.Latitude; target.Longitude = source.Longitude;
        }
        if (string.IsNullOrWhiteSpace(target.Keyword) && !string.IsNullOrWhiteSpace(source.Keyword)) { target.Keyword = source.Keyword; target.KeywordId = source.KeywordId; }
        if (source.Status == ListStatus.Open && target.Status != ListStatus.Open) { target.Status = ListStatus.Open; target.ClosedAt = null; }
        if (source.AlertTime < target.AlertTime) target.AlertTime = source.AlertTime;
        await _lists.UpdateAsync(target);

        // --- Bilder (Nextcloud) ---
        int imagesMoved = 0; var imageErrors = new List<string>();
        if (_nc.IsConfigured)
        {
            var from = _nc.BuildOperationFolder(source.AlertTime.Year, source.OperationNumber);
            var to = _nc.BuildOperationFolder(target.AlertTime.Year, target.OperationNumber);
            if (!string.Equals(from, to, StringComparison.Ordinal))
            {
                foreach (var f in await _nc.ListFilesAsync(from))
                {
                    if (await _nc.MoveAsync(from, f.Name, to)) imagesMoved++;
                    else imageErrors.Add(f.Name);
                }
            }
        }

        // --- Quelle entfernen ---
        await _reports.DeleteByOperationListIdAsync(sourceId);
        await _lists.DeleteAsync(sourceId);
        _logger.LogInformation("Einsatz {Source} in {Target} zusammengelegt: {Moved} Eintraege, {Skipped} Dubletten, {Images} Bilder.",
            source.OperationNumber, target.OperationNumber, moved, skipped, imagesMoved);

        return new MergeResult(null, moved, skipped, imagesMoved, imageErrors, source.OperationNumber);
    }

    /// <summary>Eintraege heissen "Vorname Nachname (Nummer)" - verglichen wird die Nummer, sonst der Name.</summary>
    private static string EntryKey(string nameOrId)
    {
        var o = nameOrId.LastIndexOf('('); var c = nameOrId.LastIndexOf(')');
        return o >= 0 && c > o ? nameOrId.Substring(o + 1, c - o - 1).Trim() : nameOrId.Trim();
    }

    /// <summary>Leere Text-/Bool-Felder des Ziel-Berichts aus der Quelle uebernehmen (Ziel gewinnt).</summary>
    private static void FillEmptyFields(OperationReport target, OperationReport source)
    {
        var skip = new HashSet<string> { nameof(OperationReport.Id), nameof(OperationReport.OperationListId), nameof(OperationReport.CreatedAt), nameof(OperationReport.UpdatedAt) };
        foreach (var p in typeof(OperationReport).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanRead || !p.CanWrite || skip.Contains(p.Name)) continue;
            var t = p.GetValue(target); var s = p.GetValue(source);
            if (p.PropertyType == typeof(string) && string.IsNullOrWhiteSpace((string?)t) && !string.IsNullOrWhiteSpace((string?)s)) p.SetValue(target, s);
            else if (p.PropertyType == typeof(bool) && t is false && s is true) p.SetValue(target, true);
            else if (p.PropertyType == typeof(int) && t is 0 && s is int si && si != 0) p.SetValue(target, si);
        }
    }
}
