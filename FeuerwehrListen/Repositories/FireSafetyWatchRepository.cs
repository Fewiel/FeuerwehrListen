using FeuerwehrListen.Data;
using FeuerwehrListen.DTOs;
using FeuerwehrListen.Models;
using LinqToDB;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FeuerwehrListen.Repositories
{
    public class FireSafetyWatchRepository
    {
        private readonly AppDbConnection _db;

        public FireSafetyWatchRepository(AppDbConnection db)
        {
            _db = db;
        }

        public async Task<List<FireSafetyWatchDto>> GetAllWithStatusAsync()
        {
            var watches = await _db.FireSafetyWatches.ToListAsync();
            var dtos = new List<FireSafetyWatchDto>();

            foreach (var watch in watches)
            {
                var required = await _db.FireSafetyWatchRequirements
                    .Where(r => r.FireSafetyWatchId == watch.Id)
                    .SumAsync(r => r.Amount);
                
                var assigned = await _db.FireSafetyWatchEntries
                    .Where(e => e.FireSafetyWatchId == watch.Id)
                    .CountAsync();

                dtos.Add(new FireSafetyWatchDto
                {
                    Id = watch.Id,
                    Name = watch.Name,
                    Location = watch.Location,
                    EventDateTime = watch.EventDateTime,
                    Status = watch.Status,
                    ClosedAt = watch.ClosedAt,
                    IsArchived = watch.IsArchived,
                    TotalRequired = required,
                    TotalAssigned = assigned
                });
            }

            return dtos.OrderBy(d => d.EventDateTime).ToList();
        }

        public async Task<List<FireSafetyWatch>> GetAllAsync()
        {
            return await _db.FireSafetyWatches.OrderByDescending(f => f.EventDateTime).ToListAsync();
        }
        
        public async Task<FireSafetyWatch> GetByIdAsync(int id)
        {
            return await _db.FireSafetyWatches.FirstOrDefaultAsync(f => f.Id == id);
        }

        public async Task<List<FireSafetyWatch>> GetClosedAsync()
        {
            return await _db.FireSafetyWatches.Where(x => x.Status == ListStatus.Closed && !x.IsArchived).ToListAsync();
        }

        public async Task<List<FireSafetyWatch>> GetArchivedAsync()
        {
            return await _db.FireSafetyWatches.Where(x => x.IsArchived).ToListAsync();
        }

        public async Task UpdateAsync(FireSafetyWatch watch)
        {
            await _db.UpdateAsync(watch);
        }

        public async Task InsertAsync(FireSafetyWatch watch)
        {
            await _db.InsertAsync(watch);
        }

        public async Task InsertFireSafetyWatchWithRequirements(FireSafetyWatch watch, List<FireSafetyWatchRequirement> requirements)
        {
            await _db.BeginTransactionAsync();

            try
            {
                watch.Id = await _db.InsertWithInt32IdentityAsync(watch);

                foreach (var req in requirements)
                {
                    req.FireSafetyWatchId = watch.Id;
                    await _db.InsertAsync(req);
                }

                await _db.CommitTransactionAsync();
            }
            catch
            {
                await _db.RollbackTransactionAsync();
                throw;
            }
        }
    }
}
