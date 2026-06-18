using DataAccessLayer.Context;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;
    public UserRepository(AppDbContext context) => _context = context;

    public Task<User?> GetByUsernameAsync(string username)
        => _context.Users.FirstOrDefaultAsync(u => u.Username == username || u.Email == username);

    public Task<User?> GetByIdAsync(string id)
        => _context.Users.FirstOrDefaultAsync(u => u.Id == id);

    public Task<User?> GetByVerificationTokenAsync(string token)
        => _context.Users.FirstOrDefaultAsync(u => u.EmailVerificationToken == token);

    public Task<List<User>> GetAllAsync()
        => _context.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();

    public async Task CreateAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user != null)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<long> CountAsync() => await _context.Users.LongCountAsync();

    public async Task<long> CountByRoleAsync(string role)
        => await _context.Users.LongCountAsync(u => u.Role == role);
}
