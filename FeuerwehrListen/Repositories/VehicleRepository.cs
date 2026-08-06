using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;

namespace FeuerwehrListen.Repositories;

public class VehicleRepository
{
    private readonly AppDbConnection _db;

    public VehicleRepository(AppDbConnection db)
    {
        _db = db;
    }

    public async Task<List<Vehicle>> GetAllAsync()
    {
        return await _db.Vehicles.OrderBy(x => x.Name).ToListAsync();
    }

    public async Task<List<Vehicle>> GetActiveAsync()
    {
        return await _db.Vehicles
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    /// <summary>Fahrzeuge, die beim Eintragen in Einsatzlisten/Berichten angeboten werden.
    /// Kalender-only-Fahrzeuge sind hier bewusst ausgeschlossen.</summary>
    public async Task<List<Vehicle>> GetForOperationsAsync()
    {
        return await _db.Vehicles
            .Where(x => x.IsActive && x.ShowInOperations)
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    /// <summary>Im Kalender buchbare Fahrzeuge.</summary>
    public async Task<List<Vehicle>> GetBookableAsync()
    {
        return await _db.Vehicles
            .Where(x => x.IsActive && x.IsBookable)
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<Vehicle?> GetByIdAsync(int id)
    {
        return await _db.Vehicles.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<int> CreateAsync(Vehicle vehicle)
    {
        return await _db.InsertWithInt32IdentityAsync(vehicle);
    }

    public async Task UpdateAsync(Vehicle vehicle)
    {
        await _db.UpdateAsync(vehicle);
    }

    public async Task DeleteAsync(int id)
    {
        await _db.Vehicles.Where(x => x.Id == id).DeleteAsync();
    }
}




