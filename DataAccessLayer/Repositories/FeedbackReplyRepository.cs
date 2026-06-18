using DataAccessLayer.Context;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories;

public class FeedbackReplyRepository : IFeedbackReplyRepository
{
    private readonly AppDbContext _context;
    public FeedbackReplyRepository(AppDbContext context) => _context = context;

    public async Task CreateAsync(FeedbackReply reply)
    {
        _context.FeedbackReplies.Add(reply);
        await _context.SaveChangesAsync();
    }

    public Task<List<FeedbackReply>> GetByFeedbackIdsAsync(IEnumerable<string> feedbackIds)
    {
        var ids = feedbackIds.ToList();
        return _context.FeedbackReplies
            .Where(r => ids.Contains(r.FeedbackId))
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    public Task<FeedbackReply?> GetByIdAsync(string id)
        => _context.FeedbackReplies.FirstOrDefaultAsync(r => r.Id == id);

    public async Task DeleteAsync(string id)
    {
        var reply = await _context.FeedbackReplies.FindAsync(id);
        if (reply != null)
        {
            _context.FeedbackReplies.Remove(reply);
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteByFeedbackAsync(string feedbackId)
    {
        var replies = await _context.FeedbackReplies
            .Where(r => r.FeedbackId == feedbackId).ToListAsync();
        if (replies.Count > 0)
        {
            _context.FeedbackReplies.RemoveRange(replies);
            await _context.SaveChangesAsync();
        }
    }

    public Task<int> CountAsync() => _context.FeedbackReplies.CountAsync();
}
