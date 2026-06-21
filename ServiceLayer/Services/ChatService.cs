using DataAccessLayer.Entities;
using DataAccessLayer.Repositories;
using ServiceLayer.Dtos;

using ServiceLayer.Services.Embeddings;

namespace ServiceLayer.Services;

public class ChatService : IChatService
{
    private readonly IChatRepository _chatRepo;
    private readonly IRetrievalService _retrievalService;
    private readonly IGroqService _llm;
    private readonly AutoMapper.IMapper _mapper;

    public ChatService(IChatRepository chatRepo, IRetrievalService retrievalService, IGroqService llm, AutoMapper.IMapper mapper)
    {
        _chatRepo = chatRepo;
        _retrievalService = retrievalService;
        _llm = llm;
    }

    public async Task<List<ServiceLayer.DTOs.ChatSessionDto>> GetSessionsAsync(string userId) { var entities = await _chatRepo.GetSessionsForUserAsync(userId); return _mapper.Map<List<ServiceLayer.DTOs.ChatSessionDto>>(entities); }

    public async Task<ServiceLayer.DTOs.ChatSessionDto> CreateSessionAsync(string userId, string? subjectId)
    {
        var session = new ChatSession
        {
            UserId = userId,
            SubjectId = string.IsNullOrEmpty(subjectId) ? null : subjectId,
            Title = "Cuộc hội thoại mới"
        };
        await _chatRepo.CreateSessionAsync(session);
        return _mapper.Map<ServiceLayer.DTOs.ChatSessionDto>(session);
    }

    public async Task<ServiceLayer.DTOs.ChatSessionDto?> GetSessionAsync(string sessionId) { var entity = await _chatRepo.GetSessionAsync(sessionId); return _mapper.Map<ServiceLayer.DTOs.ChatSessionDto>(entity); }
    public Task DeleteSessionAsync(string sessionId) => _chatRepo.DeleteSessionAsync(sessionId);
    public async Task<List<ServiceLayer.DTOs.ChatMessageDto>> GetMessagesAsync(string sessionId) { var entities = await _chatRepo.GetMessagesAsync(sessionId); return _mapper.Map<List<ServiceLayer.DTOs.ChatMessageDto>>(entities); }

    public async Task<ChatAnswer> AskAsync(string sessionId, string userId, string question)
    {
        var session = await _chatRepo.GetSessionAsync(sessionId)
            ?? throw new InvalidOperationException("Session không tồn tại");

        var history = await _chatRepo.GetMessagesAsync(sessionId);

        await _chatRepo.AddMessageAsync(new ChatMessage
        {
            SessionId = sessionId,
            Role = "user",
            Content = question
        });

        var searchResults = await _retrievalService.SearchAsync(question, session.SubjectId, 5);

        string answer;
        var sources = new List<ChatSource>();

        if (searchResults.Count == 0 || searchResults.All(x => x.Score < 0.2f))
        {
            answer = "Không tìm thấy thông tin này trong tài liệu đã được giảng viên cung cấp.";
        }
        else
        {
            var chunks = searchResults.Select(x => x.Chunk).ToList();
            // Deduplicate sources by DocumentId, lấy điểm cao nhất cho mỗi tài liệu
            sources = searchResults
                .GroupBy(c => c.Chunk.DocumentId)
                .Select(g =>
                {
                    var best = g.OrderByDescending(x => x.Score).First();
                    return new ChatSource
                    {
                        DocumentId = best.Chunk.DocumentId,
                        DocumentName = best.Chunk.DocumentName,
                        ChunkIndex = best.Chunk.ChunkIndex,
                        Page = best.Chunk.Page,
                        Snippet = best.Chunk.Content.Length > 300 ? best.Chunk.Content.Substring(0, 300) + "..." : best.Chunk.Content,
                        ConfidenceScore = Math.Min(best.Score, 1.0f) // Cap tối đa 100%
                    };
                })
                .OrderByDescending(s => s.ConfidenceScore)
                .ToList();

            answer = await _llm.GenerateAnswerAsync(question, chunks, history);
        }

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





