using DataAccessLayer.Context;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories;

public class AllowedEmailRepository : IAllowedEmailRepository
{
    private readonly AppDbContext _context;
    public AllowedEmailRepository(AppDbContext context) => _context = context;

    public Task<List<AllowedEmail>> GetAllAsync()
        => _context.AllowedEmails.OrderBy(a => a.Email).ToListAsync();

    public Task<bool> ExistsAsync(string email)
    {
        var normalized = email.Trim().ToLower();
        return _context.AllowedEmails.AnyAsync(a => a.Email.ToLower() == normalized);
    }

    public async Task<long> CountAsync() => await _context.AllowedEmails.LongCountAsync();

    public async Task CreateAsync(AllowedEmail allowedEmail)
    {
        _context.AllowedEmails.Add(allowedEmail);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id)
    {
        var entity = await _context.AllowedEmails.FindAsync(id);
        if (entity != null)
        {
            _context.AllowedEmails.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }
}


