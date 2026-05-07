using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;

namespace FeuerwehrListen.Services;

public class UnitAssignmentService
{
    private readonly MemberRepository _memberRepo;

    public UnitAssignmentService(MemberRepository memberRepo)
    {
        _memberRepo = memberRepo;
    }

    /// <summary>
    /// Resolves the primary unit number for a member.
    /// Priority: 1) Member.UnitNumber (explicit assignment), 2) derived from MemberNumber
    /// </summary>
    public int? ResolveUnitNumber(Member member)
    {
        // Explicit unit assignment takes priority
        if (member.UnitNumber.HasValue)
            return member.UnitNumber.Value;

        // Fallback: derive from member number
        return ResolveUnitFromNumber(member.MemberNumber);
    }

    /// <summary>
    /// Liefert ALLE Einheiten, denen ein Mitglied angehört (Multi-Unit-Membership).
    /// Reihenfolge: erst MemberUnit-Tabelle, dann Member.UnitNumber, dann Fallback aus MemberNumber.
    /// Duplikate werden entfernt.
    /// </summary>
    public async Task<List<int>> ResolveAllUnitNumbersAsync(Member member)
    {
        var result = new List<int>();

        // 1) Aus Join-Tabelle
        var fromTable = await _memberRepo.GetUnitsForMemberAsync(member.Id);
        foreach (var u in fromTable)
        {
            if (u >= 1 && u <= 9 && !result.Contains(u))
                result.Add(u);
        }

        // 2) Primäreinheit (falls noch nicht enthalten)
        if (member.UnitNumber.HasValue && !result.Contains(member.UnitNumber.Value))
            result.Add(member.UnitNumber.Value);

        // 3) Fallback aus MemberNumber, falls überhaupt nichts hinterlegt ist
        if (result.Count == 0)
        {
            var derived = ResolveUnitFromNumber(member.MemberNumber);
            if (derived.HasValue)
                result.Add(derived.Value);
        }

        return result.OrderBy(x => x).ToList();
    }

    /// <summary>
    /// Synchroner Helfer: nimmt Member + bereits geladene Einheits-Liste und
    /// fügt Primäreinheit/Fallback hinzu, sofern leer.
    /// Nützlich, wenn die MemberUnit-Daten bereits per Bulk-Load (Dictionary) vorliegen.
    /// </summary>
    public List<int> CombineUnitNumbers(Member member, IEnumerable<int>? fromTable)
    {
        var result = new List<int>();
        if (fromTable != null)
        {
            foreach (var u in fromTable)
            {
                if (u >= 1 && u <= 9 && !result.Contains(u))
                    result.Add(u);
            }
        }

        if (member.UnitNumber.HasValue && !result.Contains(member.UnitNumber.Value))
            result.Add(member.UnitNumber.Value);

        if (result.Count == 0)
        {
            var derived = ResolveUnitFromNumber(member.MemberNumber);
            if (derived.HasValue)
                result.Add(derived.Value);
        }

        return result.OrderBy(x => x).ToList();
    }

    /// <summary>
    /// Legacy: derives unit from member number only (1000-1999 = Unit 1, etc.)
    /// </summary>
    public int? ResolveUnitFromNumber(string memberNumber)
    {
        if (!int.TryParse(memberNumber, out var number) || number < 1)
            return null;

        if (number <= 999)
            return null;

        var unit = number / 1000;
        if (unit < 1 || unit > 9)
            return null;

        return unit;
    }

    /// <summary>
    /// Backwards-compatible overload — accepts just a member number string.
    /// Used by code that doesn't have the full Member object.
    /// </summary>
    public int? ResolveUnitNumber(string memberNumber)
    {
        return ResolveUnitFromNumber(memberNumber);
    }
}
