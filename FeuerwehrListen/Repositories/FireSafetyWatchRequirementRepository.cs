using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FeuerwehrListen.Repositories
{
    public class FireSafetyWatchRequirementRepository
    {
        private readonly AppDbConnection _db;

        public FireSafetyWatchRequirementRepository(AppDbConnection db)
        {
            _db = db;
        }

        public async Task<List<FireSafetyWatchRequirement>> GetByWatchIdAsync(int watchId)
        {
            return await _db.FireSafetyWatchRequirements
                .LoadWith(r => r.Vehicle)
                .LoadWith(r => r.FunctionDef)
                .Where(r => r.FireSafetyWatchId == watchId)
                .ToListAsync();
        }
    }
}
