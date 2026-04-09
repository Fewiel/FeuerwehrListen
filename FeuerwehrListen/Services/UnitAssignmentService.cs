using FeuerwehrListen.Models;

namespace FeuerwehrListen.Services;

public class UnitAssignmentService
{
    /// <summary>
    /// Resolves the unit number for a member.
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
