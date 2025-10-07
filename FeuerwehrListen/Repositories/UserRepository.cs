using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;

namespace FeuerwehrListen.Repositories;

public class UserRepository
{
    private readonly AppDbConnection _db;

    public UserRepository(AppDbConnection db)
    {
        _db = db;
    }

    public async Task<List<User>> GetAllAsync()
    {
        return await _db.Users.OrderBy(x => x.LastName).ToListAsync();
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        return await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _db.Users.FirstOrDefaultAsync(x => x.Username == username);
    }

    public async Task<int> CreateAsync(User user)
    {
        return await _db.InsertWithInt32IdentityAsync(user);
    }

    public async Task UpdateAsync(User user)
    {
        await _db.UpdateAsync(user);
    }

    public async Task DeleteAsync(int id)
    {
        await _db.Users.Where(x => x.Id == id).DeleteAsync();
    }
}

