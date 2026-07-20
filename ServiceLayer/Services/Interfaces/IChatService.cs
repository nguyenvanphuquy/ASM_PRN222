using DataAccessLayer.Entities;
using ServiceLayer.Dtos;

namespace ServiceLayer.Services.Interfaces;

public interface IChatService
{
    Task<List<DTOs.ChatSessionDto>> GetSessionsAsync(string userId);
    Task<DTOs.ChatSessionDto> CreateSessionAsync(string userId, string? subjectId);
    Task<DTOs.ChatSessionDto?> GetSessionAsync(string sessionId);
    Task DeleteSessionAsync(string sessionId);
    Task<List<DTOs.ChatMessageDto>> GetMessagesAsync(string sessionId);
    Task<ChatAnswer> AskAsync(string sessionId, string userId, string question, string? language = null);
}



