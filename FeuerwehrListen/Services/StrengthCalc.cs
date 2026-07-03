using FeuerwehrListen.Models;

namespace FeuerwehrListen.Services;

/// <summary>
/// Berechnet die Mannschaftsstärke aus Einsatzliste-Einträgen anhand der
/// Zählstelle (StrengthPosition) der zugewiesenen Funktionen.
/// Position: 1 = Führer/Zugführer, 2 = Helfer/Gruppenführer, 3 = Mannschaft.
/// Einträge ohne Funktion zählen als Mannschaft.
/// </summary>
public static class StrengthCalc
{
    /// <summary>Zählstelle eines Eintrags = stärkste (kleinste) Position seiner Funktionen, sonst Mannschaft (3).</summary>
    public static int PositionOf(OperationEntry entry, Dictionary<int, List<OperationFunctionDef>> funcsByEntry)
    {
        if (funcsByEntry.TryGetValue(entry.Id, out var funcs) && funcs.Count > 0)
            return funcs.Min(f => (int)f.StrengthPosition);
        return (int)StrengthPosition.Mannschaft;
    }

    /// <summary>(Führer, Helfer, Mannschaft, Gesamt) über alle Einträge.</summary>
    public static (int p1, int p2, int p3, int total) Total(
        IEnumerable<OperationEntry> entries, Dictionary<int, List<OperationFunctionDef>> funcsByEntry)
    {
        int p1 = 0, p2 = 0, p3 = 0;
        foreach (var e in entries)
        {
            var pos = PositionOf(e, funcsByEntry);
            if (pos == 1) p1++;
            else if (pos == 2) p2++;
            else p3++;
        }
        return (p1, p2, p3, p1 + p2 + p3);
    }

    /// <summary>Volle Stärke als „F/H/M/G", z. B. 1/4/27/32.</summary>
    public static string FormatTotal(IEnumerable<OperationEntry> entries, Dictionary<int, List<OperationFunctionDef>> funcsByEntry)
    {
        var t = Total(entries, funcsByEntry);
        return $"{t.p1}/{t.p2}/{t.p3}/{t.total}";
    }

    /// <summary>Fahrzeug-Stärke als „Führer/Mannschaft" (Führer = Position 1+2), z. B. 1/8.</summary>
    public static string VehicleFuehrerMannschaft(IEnumerable<OperationEntry> vehicleEntries, Dictionary<int, List<OperationFunctionDef>> funcsByEntry)
    {
        int fuehrer = 0, mannschaft = 0;
        foreach (var e in vehicleEntries)
        {
            var pos = PositionOf(e, funcsByEntry);
            if (pos <= 2) fuehrer++;
            else mannschaft++;
        }
        return $"{fuehrer}/{mannschaft}";
    }
}
