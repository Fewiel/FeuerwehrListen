using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;

namespace FeuerwehrListen.Repositories;

public class RoomRepository
{
    private readonly AppDbConnection _db;

    public RoomRepository(AppDbConnection db)
    {
        _db = db;
    }

    public async Task<List<Room>> GetAllAsync()
    {
        return await _db.Rooms.OrderBy(x => x.Name).ToListAsync();
    }

    public async Task<List<Room>> GetActiveAsync()
    {
        return await _db.Rooms.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
    }

    public async Task<Room?> GetByIdAsync(int id)
    {
        return await _db.Rooms.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<int> CreateAsync(Room room)
    {
        return await _db.InsertWithInt32IdentityAsync(room);
    }

    public async Task UpdateAsync(Room room)
    {
        await _db.UpdateAsync(room);
    }

    public async Task DeleteAsync(int id)
    {
        await _db.Rooms.Where(x => x.Id == id).DeleteAsync();
    }
}
