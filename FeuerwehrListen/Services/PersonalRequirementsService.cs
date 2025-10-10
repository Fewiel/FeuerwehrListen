using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;

namespace FeuerwehrListen.Services;

public class PersonalRequirementsService
{
    private readonly PersonalRequirementRepository _requirementRepo;
    private readonly OperationEntryFunctionRepository _entryFunctionRepo;
    private readonly OperationFunctionRepository _functionRepo;
    private readonly OperationEntryRepository _entryRepo;

    public PersonalRequirementsService(
        PersonalRequirementRepository requirementRepo,
        OperationEntryFunctionRepository entryFunctionRepo,
        OperationFunctionRepository functionRepo,
        OperationEntryRepository entryRepo)
    {
        _requirementRepo = requirementRepo;
        _entryFunctionRepo = entryFunctionRepo;
        _functionRepo = functionRepo;
        _entryRepo = entryRepo;
    }

    public async Task<PersonalRequirementsValidationResult> ValidateRequirementsAsync(int operationListId, int keywordId)
    {
        var result = new PersonalRequirementsValidationResult
        {
            IsValid = true,
            MissingRequirements = new List<MissingRequirement>()
        };

        // Get all entries for this operation
        var entries = await _entryRepo.GetByListIdAsync(operationListId);
        if (!entries.Any())
        {
            return result;
        }

        // Get personal requirements for this keyword
        var requirements = await _requirementRepo.GetByKeywordIdAsync(keywordId);
        if (!requirements.Any())
        {
            return result;
        }

        // Get all function definitions
        var allFunctions = await _functionRepo.GetAllAsync();
        var functionMap = allFunctions.ToDictionary(f => f.Id, f => f.Name);

        // Count personnel by function
        var personnelCounts = new Dictionary<int, int>();
        var entryIds = entries.Select(e => e.Id).ToList();
        var functionsByEntry = await _entryFunctionRepo.GetFunctionsForEntriesAsync(entryIds);
        
        foreach (var entryFunctions in functionsByEntry.Values)
        {
            foreach (var function in entryFunctions)
            {
                if (personnelCounts.ContainsKey(function.Id))
                    personnelCounts[function.Id]++;
                else
                    personnelCounts[function.Id] = 1;
            }
        }

        // Check each requirement
        foreach (var requirement in requirements)
        {
            var currentCount = personnelCounts.GetValueOrDefault(requirement.FunctionDefId, 0);
            var requiredCount = requirement.MinimumCount;
            var functionName = functionMap.GetValueOrDefault(requirement.FunctionDefId, "Unbekannte Funktion");

            if (currentCount < requiredCount)
            {
                result.MissingRequirements.Add(new MissingRequirement
                {
                    FunctionName = functionName,
                    CurrentCount = currentCount,
                    RequiredCount = requiredCount,
                    IsRequired = requirement.IsRequired
                });

                if (requirement.IsRequired)
                {
                    result.IsValid = false;
                }
            }
        }

        return result;
    }

    public async Task<List<PersonalRequirement>> GetRequirementsForKeywordAsync(int keywordId)
    {
        return await _requirementRepo.GetByKeywordIdAsync(keywordId);
    }

    public async Task<Dictionary<int, int>> GetMinimumRequirementsAsync(int keywordId)
    {
        return await _requirementRepo.GetMinimumRequirementsAsync(keywordId);
    }
}

