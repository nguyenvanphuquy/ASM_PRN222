using DataAccessLayer.Entities;

namespace DataAccessLayer.Repositories;

public interface IChatRepository
{
    Task<List<ChatSession>> GetSessionsForUserAsync(string userId);
    Task<ChatSession?> GetSessionAsync(string sessionId);
    Task CreateSessionAsync(ChatSession session);
    Task UpdateSessionAsync(ChatSession session);
    Task DeleteSessionAsync(string sessionId);

    Task<List<ChatMessage>> GetMessagesAsync(string sessionId);
    Task AddMessageAsync(ChatMessage message);

    Task<int> CountSessionsAsync();
    Task<int> CountMessagesAsync();
}
