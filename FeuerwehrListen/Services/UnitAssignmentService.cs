namespace FeuerwehrListen.Services;

public class UnitAssignmentService
{
    public int? ResolveUnitNumber(string memberNumber)
    {
        if (!int.TryParse(memberNumber, out var number) || number < 1)
        {
            return null;
        }

        if (number <= 999)
        {
            return null;
        }

        var unit = number / 1000;
        if (unit < 1 || unit > 9)
        {
            return null;
        }

        return unit;
    }
}
