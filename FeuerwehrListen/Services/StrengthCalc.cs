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

    /// <summary>„Ohne Fahrzeug" bzw. leer – zählt nicht als Fahrzeug.</summary>
    public static bool IsNoVehicle(string? vehicle)
        => string.IsNullOrWhiteSpace(vehicle)
           || vehicle.Trim().Equals("Ohne Fahrzeug", StringComparison.OrdinalIgnoreCase);

    /// <summary>Parst einen „Führer/Mannschaft"-String (z. B. „1/8") zu (Führer, Mannschaft).</summary>
    public static (int fuehrer, int mannschaft) ParseFM(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return (0, 0);
        var parts = s.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        int f = parts.Length > 0 && int.TryParse(parts[0], out var a) ? a : 0;
        int m = parts.Length > 1 && int.TryParse(parts[1], out var b) ? b : 0;
        return (f, m);
    }

    /// <summary>Summiert mehrere „Führer/Mannschaft"-Strings zu „F/M/Gesamt".</summary>
    public static string SumFM(IEnumerable<string?> strengthStrings)
    {
        int f = 0, m = 0;
        foreach (var s in strengthStrings)
        {
            var (a, b) = ParseFM(s);
            f += a; m += b;
        }
        return $"{f}/{m}/{f + m}";
    }

    /// <summary>
    /// Volle Stärke im laufenden Einsatz: Fahrzeug-Stärken (manuell) + externe Kräfte, als „F/M/Gesamt".
    /// Sobald für ein Fahrzeug gescannte Einträge vorliegen, wird der Ist-Wert daraus bevorzugt (keine Doppelzählung).
    /// „Ohne Fahrzeug" wird ignoriert.
    /// </summary>
    public static string CombinedTotal(
        IEnumerable<OperationEntry> entries,
        Dictionary<int, List<OperationFunctionDef>> funcsByEntry,
        IEnumerable<(string? VehicleName, string? Staerke)> vehicleStrengths,
        IEnumerable<string?> externalStrengths)
    {
        var fmStrings = new List<string?>();

        var entriesByVehicle = entries
            .Where(e => !IsNoVehicle(e.Vehicle))
            .GroupBy(e => e.Vehicle.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IEnumerable<OperationEntry>)g.ToList(), StringComparer.OrdinalIgnoreCase);

        var handled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var vs in vehicleStrengths)
        {
            if (IsNoVehicle(vs.VehicleName)) continue;
            var name = vs.VehicleName!.Trim();
            if (handled.Contains(name)) continue;
            handled.Add(name);
            fmStrings.Add(entriesByVehicle.TryGetValue(name, out var ents)
                ? VehicleFuehrerMannschaft(ents, funcsByEntry)   // Ist-Wert aus Einsatzliste bevorzugen
                : vs.Staerke);                                    // sonst manuelle Stärke
        }

        // Fahrzeuge, die gescannt wurden, aber keine manuelle Stärke haben
        foreach (var kv in entriesByVehicle)
        {
            if (handled.Contains(kv.Key)) continue;
            fmStrings.Add(VehicleFuehrerMannschaft(kv.Value, funcsByEntry));
        }

        fmStrings.AddRange(externalStrengths);
        return SumFM(fmStrings);
    }
}
