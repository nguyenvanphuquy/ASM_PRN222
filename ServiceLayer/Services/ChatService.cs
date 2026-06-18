using DataAccessLayer.Entities;
using DataAccessLayer.Repositories;
using ServiceLayer.Dtos;

namespace ServiceLayer.Services;

public class ChatService : IChatService
{
    private readonly IChatRepository _chatRepo;
    private readonly IDocumentChunkRepository _chunkRepo;
    private readonly IGroqService _llm;

    public ChatService(IChatRepository chatRepo, IDocumentChunkRepository chunkRepo, IGroqService llm)
    {
        _chatRepo = chatRepo;
        _chunkRepo = chunkRepo;
        _llm = llm;
    }

    public Task<List<ChatSession>> GetSessionsAsync(string userId) => _chatRepo.GetSessionsForUserAsync(userId);

    public async Task<ChatSession> CreateSessionAsync(string userId, string? subjectId)
    {
        var session = new ChatSession
        {
            UserId = userId,
            SubjectId = string.IsNullOrEmpty(subjectId) ? null : subjectId,
            Title = "Cuộc hội thoại mới"
        };
        await _chatRepo.CreateSessionAsync(session);
        return session;
    }

    public Task<ChatSession?> GetSessionAsync(string sessionId) => _chatRepo.GetSessionAsync(sessionId);
    public Task DeleteSessionAsync(string sessionId) => _chatRepo.DeleteSessionAsync(sessionId);
    public Task<List<ChatMessage>> GetMessagesAsync(string sessionId) => _chatRepo.GetMessagesAsync(sessionId);

    public async Task<ChatAnswer> AskAsync(string sessionId, string userId, string question)
    {
        var session = await _chatRepo.GetSessionAsync(sessionId)
            ?? throw new InvalidOperationException("Session không tồn tại");

        // Load history BEFORE saving the new question so we feed only prior turns
        var history = await _chatRepo.GetMessagesAsync(sessionId);

        await _chatRepo.AddMessageAsync(new ChatMessage
        {
            SessionId = sessionId,
            Role = "user",
            Content = question
        });

        var chunks = await _chunkRepo.SearchAsync(question, session.SubjectId, limit: 5);

        var answer = await _llm.GenerateAnswerAsync(question, chunks, history);

        var sources = chunks.Select(c => new ChatSource
        {
            DocumentId = c.DocumentId,
            DocumentName = c.DocumentName,
            ChunkIndex = c.ChunkIndex,
            Page = c.Page,
            Snippet = c.Content.Length > 300 ? c.Content.Substring(0, 300) + "..." : c.Content
        }).ToList();

        await _chatRepo.AddMessageAsync(new ChatMessage
        {
            SessionId = sessionId,
            Role = "assistant",
            Content = answer,
            Sources = sources
        });

        if (session.Title == "Cuộc hội thoại mới")
            session.Title = question.Length > 60 ? question.Substring(0, 60) + "..." : question;

        await _chatRepo.UpdateSessionAsync(session);

        return new ChatAnswer(answer, sources);
    }
}
