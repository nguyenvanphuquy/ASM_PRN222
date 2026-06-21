using DataAccessLayer.Entities;
using ServiceLayer.Dtos;

namespace ServiceLayer.Services;

public interface IChatService
{
    Task<List<ServiceLayer.DTOs.ChatSessionDto>> GetSessionsAsync(string userId);
    Task<ServiceLayer.DTOs.ChatSessionDto> CreateSessionAsync(string userId, string? subjectId);
    Task<ServiceLayer.DTOs.ChatSessionDto?> GetSessionAsync(string sessionId);
    Task DeleteSessionAsync(string sessionId);
    Task<List<ServiceLayer.DTOs.ChatMessageDto>> GetMessagesAsync(string sessionId);
    Task<ChatAnswer> AskAsync(string sessionId, string userId, string question);
}



