namespace FeuerwehrListen.Client.Models;

/// <param name="DeviceName">Gesetzt, wenn der Aufruf von einem dauerhaft angemeldeten Geraetekonto kommt.</param>
public record AppContextDto(string AppName, string? LogoUrl, ModuleFlags Modules, List<UnitLabel>? UnitLabels = null, bool LoginRequired = false, string? DeviceName = null, string? Version = null);

public record UnitLabel(int Number, string Label);

public record KeywordDto(string Name, string? Description);

/// <param name="FireSafetyLink">Wachen ueber den Direktlink /wache/{guid}.</param>
/// <param name="FullAccess">Vollzugang: auch "Listen &amp; Daten" (abgeschlossene Listen, Archiv).</param>
public record ModuleFlags(bool Attendance, bool Operations, bool FireSafety, bool Defects, bool Calendar = false,
    bool FireSafetyLink = true, bool FullAccess = true, bool Leadership = false)
{
    /// <summary>Darf eine Seiten-Route aufgerufen werden? Spiegelt
    /// NetworkAccessService.ModuleForPagePath auf dem Server.</summary>
    public bool AllowsPage(string relativePath)
    {
        var p = "/" + relativePath.Split('?', '#')[0].Trim('/').ToLowerInvariant();
        if (p.StartsWith("/wache/")) return FireSafetyLink;
        if (p == "/create-attendance" || p.StartsWith("/attendance/")) return Attendance;
        if (p == "/create-operation" || p.StartsWith("/operation/")) return Operations;
        if (p == "/firesafetywatches" || p.StartsWith("/firesafetywatches/")) return FireSafety;
        if (p == "/defects") return Defects;
        if (p == "/kalender") return Calendar;
        if (p == "/fuehrungskreis" || p.StartsWith("/fuehrungskreis/")) return Leadership;
        if (p == "/admin/closed-lists" || p == "/admin/archive") return FullAccess;
        return true;
    }
}

public record FeedbackOperationDto(int Id, string Keyword, string Address, string Number, DateTime Time);
