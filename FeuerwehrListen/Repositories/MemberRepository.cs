using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;

namespace FeuerwehrListen.Repositories;

public class MemberRepository
{
    private readonly AppDbConnection _db;

    public MemberRepository(AppDbConnection db)
    {
        _db = db;
    }

    public async Task<List<Member>> GetAllAsync()
    {
        return await _db.Members.OrderBy(x => x.LastName).ToListAsync();
    }

    public async Task<List<Member>> GetActiveAsync()
    {
        return await _db.Members.Where(x => x.IsActive).OrderBy(x => x.LastName).ToListAsync();
    }

    public async Task<Member?> GetByIdAsync(int id)
    {
        return await _db.Members.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<Member?> GetByMemberNumberAsync(string memberNumber)
    {
        return await _db.Members.FirstOrDefaultAsync(x => x.MemberNumber == memberNumber);
    }

    public async Task<Member?> FindByNameOrNumberAsync(string searchTerm)
    {
        var term = searchTerm.Trim();
        
        var member = await _db.Members
            .Where(x => x.IsActive && (
                x.MemberNumber == term ||
                (x.FirstName + " " + x.LastName).Contains(term) ||
                (x.LastName + " " + x.FirstName).Contains(term)
            ))
            .FirstOrDefaultAsync();
        
        return member;
    }

    public async Task<List<Member>> SearchAsync(string query, int max = 10)
    {
        var term = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(term))
        {
            return new List<Member>();
        }
        return await _db.Members
            .Where(x => x.IsActive && (
                x.MemberNumber.Contains(term) ||
                (x.FirstName + " " + x.LastName).Contains(term) ||
                (x.LastName + " " + x.FirstName).Contains(term)
            ))
            .OrderBy(x => x.LastName)
            .Take(max)
            .ToListAsync();
    }

    public async Task<int> CreateAsync(Member member)
    {
        return await _db.InsertWithInt32IdentityAsync(member);
    }

    public async Task UpdateAsync(Member member)
    {
        await _db.UpdateAsync(member);
    }

    public async Task DeleteAsync(int id)
    {
        // Zuerst Einheits-Zuordnungen löschen, dann das Mitglied selbst.
        await _db.MemberUnits.Where(x => x.MemberId == id).DeleteAsync();
        await _db.Members.Where(x => x.Id == id).DeleteAsync();
    }

    // ---------- Multi-Unit Membership ----------

    /// <summary>
    /// Liefert alle Einheits-Nummern, denen das Mitglied zugeordnet ist (aus MemberUnit).
    /// </summary>
    public async Task<List<int>> GetUnitsForMemberAsync(int memberId)
    {
        return await _db.MemberUnits
            .Where(x => x.MemberId == memberId)
            .Select(x => x.UnitNumber)
            .OrderBy(x => x)
            .ToListAsync();
    }

    /// <summary>
    /// Liefert alle MemberIds, die einer bestimmten Einheit zugeordnet sind.
    /// </summary>
    public async Task<List<int>> GetMemberIdsForUnitAsync(int unitNumber)
    {
        return await _db.MemberUnits
            .Where(x => x.UnitNumber == unitNumber)
            .Select(x => x.MemberId)
            .ToListAsync();
    }

    /// <summary>
    /// Liefert eine Map MemberId → Liste von Einheits-Nummern für die übergebenen MemberIds.
    /// Effizient, um in einem Rutsch alle Zuordnungen zu laden.
    /// </summary>
    public async Task<Dictionary<int, List<int>>> GetUnitsForMembersAsync(IEnumerable<int> memberIds)
    {
        var ids = memberIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<int, List<int>>();

        var rows = await _db.MemberUnits
            .Where(x => ids.Contains(x.MemberId))
            .ToListAsync();

        return rows
            .GroupBy(x => x.MemberId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.UnitNumber).OrderBy(u => u).ToList());
    }

    /// <summary>
    /// Setzt die Einheits-Zuordnungen für ein Mitglied (überschreibt komplett).
    /// Wenn primaryUnit gesetzt ist, wird Member.UnitNumber entsprechend gesetzt
    /// (für Abwärtskompatibilität).
    /// </summary>
    public async Task SetUnitsForMemberAsync(int memberId, IEnumerable<int> unitNumbers, int? primaryUnit = null)
    {
        var distinct = unitNumbers
            .Where(u => u >= 1 && u <= 9)
            .Distinct()
            .ToList();

        await _db.MemberUnits.Where(x => x.MemberId == memberId).DeleteAsync();

        foreach (var unit in distinct)
        {
            await _db.InsertAsync(new MemberUnit
            {
                MemberId = memberId,
                UnitNumber = unit
            });
        }

        // Primäreinheit aktualisieren (Member.UnitNumber)
        var member = await GetByIdAsync(memberId);
        if (member != null)
        {
            int? newPrimary;
            if (primaryUnit.HasValue && distinct.Contains(primaryUnit.Value))
                newPrimary = primaryUnit.Value;
            else if (distinct.Count > 0)
                newPrimary = distinct.First();
            else
                newPrimary = null;

            if (member.UnitNumber != newPrimary)
            {
                member.UnitNumber = newPrimary;
                await _db.UpdateAsync(member);
            }
        }
    }

    /// <summary>
    /// Fügt eine einzelne Einheits-Zuordnung hinzu (idempotent).
    /// </summary>
    public async Task AddUnitToMemberAsync(int memberId, int unitNumber)
    {
        if (unitNumber < 1 || unitNumber > 9)
            return;

        var exists = await _db.MemberUnits
            .AnyAsync(x => x.MemberId == memberId && x.UnitNumber == unitNumber);
        if (exists)
            return;

        await _db.InsertAsync(new MemberUnit
        {
            MemberId = memberId,
            UnitNumber = unitNumber
        });
    }
}


