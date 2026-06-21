using DataAccessLayer.Context;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories;

public class ChatRepository : IChatRepository
{
    private readonly AppDbContext _context;
    public ChatRepository(AppDbContext context) => _context = context;

    public Task<List<ChatSession>> GetSessionsForUserAsync(string userId)
        => _context.ChatSessions.Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt).ToListAsync();

    public Task<ChatSession?> GetSessionAsync(string sessionId)
        => _context.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId);

    public async Task CreateSessionAsync(ChatSession session)
    {
        _context.ChatSessions.Add(session);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateSessionAsync(ChatSession session)
    {
        session.UpdatedAt = DateTime.UtcNow;
        _context.ChatSessions.Update(session);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteSessionAsync(string sessionId)
    {
        var messages = await _context.ChatMessages
            .Where(m => m.SessionId == sessionId).ToListAsync();
        _context.ChatMessages.RemoveRange(messages);

        var session = await _context.ChatSessions.FindAsync(sessionId);
        if (session != null) _context.ChatSessions.Remove(session);

        await _context.SaveChangesAsync();
    }

    public Task<List<ChatMessage>> GetMessagesAsync(string sessionId)
        => _context.ChatMessages.Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt).ToListAsync();

    public async Task AddMessageAsync(ChatMessage message)
    {
        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();
    }

    public Task<int> CountSessionsAsync() => _context.ChatSessions.CountAsync();
    public Task<int> CountMessagesAsync() => _context.ChatMessages.CountAsync();
}


