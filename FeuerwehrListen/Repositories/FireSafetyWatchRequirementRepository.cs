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

        /// <summary>Fahrzeugnamen je Wache fuer mehrere Wachen auf einmal - vermeidet N+1
        /// in Uebersichten. Anforderungen ohne Fahrzeug bleiben aussen vor.</summary>
        public async Task<Dictionary<int, List<string>>> GetVehicleNamesForWatchesAsync(ICollection<int> watchIds)
        {
            var result = new Dictionary<int, List<string>>();
            if (watchIds.Count == 0) return result;

            var rows = await (
                from r in _db.FireSafetyWatchRequirements
                join v in _db.Vehicles on r.VehicleId equals v.Id
                where watchIds.Contains(r.FireSafetyWatchId)
                select new { r.FireSafetyWatchId, v.Name }).ToListAsync();

            foreach (var row in rows)
            {
                if (!result.TryGetValue(row.FireSafetyWatchId, out var list))
                    result[row.FireSafetyWatchId] = list = new List<string>();
                if (!list.Contains(row.Name)) list.Add(row.Name);
            }
            foreach (var list in result.Values) list.Sort(StringComparer.CurrentCulture);
            return result;
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
