using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;

namespace FeuerwehrListen.Repositories;

public class OperationEntryFunctionRepository
{
    private readonly AppDbConnection _db;

    public OperationEntryFunctionRepository(AppDbConnection db)
    {
        _db = db;
    }

    public Task<List<OperationEntryFunction>> GetByEntryIdAsync(int entryId)
    {
        return _db.OperationEntryFunctions.Where(x => x.OperationEntryId == entryId).ToListAsync();
    }

    public async Task<Dictionary<int, List<OperationFunctionDef>>> GetFunctionsForEntriesAsync(IEnumerable<int> entryIds)
    {
        var ids = entryIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, List<OperationFunctionDef>>();

        var q = from map in _db.OperationEntryFunctions
                join def in _db.OperationFunctionDefs on map.FunctionDefId equals def.Id
                where ids.Contains(map.OperationEntryId)
                select new { map.OperationEntryId, Def = def };

        var rows = await q.ToListAsync();
        return rows
            .GroupBy(r => r.OperationEntryId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Def).OrderBy(d => d.Name).ToList());
    }

    public async Task SetFunctionsForEntryAsync(int entryId, IEnumerable<int> functionDefIds)
    {
        await _db.OperationEntryFunctions.Where(x => x.OperationEntryId == entryId).DeleteAsync();
        var toInsert = functionDefIds.Distinct().Select(fid => new OperationEntryFunction
        {
            OperationEntryId = entryId,
            FunctionDefId = fid
        }).ToList();

        foreach (var row in toInsert)
        {
            await _db.InsertWithInt32IdentityAsync(row);
        }
    }
}


