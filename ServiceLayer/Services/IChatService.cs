using DataAccessLayer.Entities;
using ServiceLayer.Dtos;

namespace ServiceLayer.Services;

public interface IChatService
{
    Task<List<ChatSession>> GetSessionsAsync(string userId);
    Task<ChatSession> CreateSessionAsync(string userId, string? subjectId);
    Task<ChatSession?> GetSessionAsync(string sessionId);
    Task DeleteSessionAsync(string sessionId);
    Task<List<ChatMessage>> GetMessagesAsync(string sessionId);
    Task<ChatAnswer> AskAsync(string sessionId, string userId, string question);
}
