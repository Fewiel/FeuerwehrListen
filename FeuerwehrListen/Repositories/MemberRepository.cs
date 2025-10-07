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
        await _db.Members.Where(x => x.Id == id).DeleteAsync();
    }
}


