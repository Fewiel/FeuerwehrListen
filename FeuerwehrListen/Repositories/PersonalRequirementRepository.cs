using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;

namespace FeuerwehrListen.Repositories;

public class PersonalRequirementRepository
{
    private readonly AppDbConnection _connection;

    public PersonalRequirementRepository(AppDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<List<PersonalRequirement>> GetByKeywordIdAsync(int keywordId)
    {
        return await _connection.PersonalRequirements
            .Where(pr => pr.KeywordId == keywordId)
            .ToListAsync();
    }

    public async Task<List<PersonalRequirement>> GetByFunctionDefIdAsync(int functionDefId)
    {
        return await _connection.PersonalRequirements
            .Where(pr => pr.FunctionDefId == functionDefId)
            .ToListAsync();
    }

    public async Task<PersonalRequirement?> GetByIdAsync(int id)
    {
        return await _connection.PersonalRequirements
            .FirstOrDefaultAsync(pr => pr.Id == id);
    }

    public async Task<int> CreateAsync(PersonalRequirement requirement)
    {
        return await _connection.InsertWithInt32IdentityAsync(requirement);
    }

    public async Task UpdateAsync(PersonalRequirement requirement)
    {
        await _connection.UpdateAsync(requirement);
    }

    public async Task DeleteAsync(int id)
    {
        await _connection.PersonalRequirements
            .Where(pr => pr.Id == id)
            .DeleteAsync();
    }

    public async Task DeleteByKeywordIdAsync(int keywordId)
    {
        await _connection.PersonalRequirements
            .Where(pr => pr.KeywordId == keywordId)
            .DeleteAsync();
    }

    public async Task<List<PersonalRequirement>> GetRequirementsWithDetailsAsync(int keywordId)
    {
        return await (from pr in _connection.PersonalRequirements
                     join fd in _connection.OperationFunctionDefs on pr.FunctionDefId equals fd.Id
                     where pr.KeywordId == keywordId
                     select pr)
                     .ToListAsync();
    }

    public async Task<Dictionary<int, int>> GetMinimumRequirementsAsync(int keywordId)
    {
        var requirements = await _connection.PersonalRequirements
            .Where(pr => pr.KeywordId == keywordId)
            .ToListAsync();

        return requirements.ToDictionary(pr => pr.FunctionDefId, pr => pr.MinimumCount);
    }
}
