using DataAccessLayer.Context;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories;

public class FeedbackRepository : IFeedbackRepository
{
    private readonly AppDbContext _context;
    public FeedbackRepository(AppDbContext context) => _context = context;

    public Task<List<Feedback>> GetAllAsync()
        => _context.Feedbacks.OrderByDescending(f => f.CreatedAt).ToListAsync();

    public Task<List<Feedback>> GetByUserAsync(string userId)
        => _context.Feedbacks.Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt).ToListAsync();

    public Task<Feedback?> GetByIdAsync(string id)
        => _context.Feedbacks.FirstOrDefaultAsync(f => f.Id == id);

    public async Task CreateAsync(Feedback feedback)
    {
        _context.Feedbacks.Add(feedback);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Feedback feedback)
    {
        _context.Feedbacks.Update(feedback);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id)
    {
        var feedback = await _context.Feedbacks.FindAsync(id);
        if (feedback != null)
        {
            _context.Feedbacks.Remove(feedback);
            await _context.SaveChangesAsync();
        }
    }

    public Task<int> CountAsync() => _context.Feedbacks.CountAsync();

    public async Task<double> AverageRatingAsync()
        => await _context.Feedbacks.AnyAsync()
            ? await _context.Feedbacks.AverageAsync(f => (double)f.Rating)
            : 0;
}


