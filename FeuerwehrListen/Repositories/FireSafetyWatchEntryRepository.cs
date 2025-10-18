using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FeuerwehrListen.Repositories
{
    public class FireSafetyWatchEntryRepository
    {
        private readonly AppDbConnection _db;

        public FireSafetyWatchEntryRepository(AppDbConnection db)
        {
            _db = db;
        }

        public async Task<List<FireSafetyWatchEntry>> GetByWatchIdAsync(int watchId)
        {
            return await _db.FireSafetyWatchEntries
                .LoadWith(e => e.Member)
                .Where(e => e.FireSafetyWatchId == watchId)
                .ToListAsync();
        }

        public async Task InsertAsync(FireSafetyWatchEntry entry)
        {
            await _db.InsertAsync(entry);
        }

        public async Task DeleteAsync(int entryId)
        {
            await _db.FireSafetyWatchEntries.Where(e => e.Id == entryId).DeleteAsync();
        }
    }
}
