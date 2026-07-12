using DataAccessLayer.Entities;
using DataAccessLayer.Repositories;
using DataAccessLayer.Repositories.Interfaces;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Embeddings;
using ServiceLayer.Services.Interfaces;

namespace ServiceLayer.Services.Implementations;

public class ChatService : IChatService
{
    private readonly IChatRepository _chatRepo;
    private readonly IUserRepository _userRepo;
    private readonly IRetrievalService _retrievalService;
    private readonly IGroqService _llm;
    private readonly IBillingService _billing;
    private readonly AutoMapper.IMapper _mapper;

    public ChatService(
        IChatRepository chatRepo,
        IUserRepository userRepo,
        IRetrievalService retrievalService,
        IGroqService llm,
        IBillingService billing,
        AutoMapper.IMapper mapper)
    {
        _chatRepo = chatRepo;
        _userRepo = userRepo;
        _retrievalService = retrievalService;
        _llm = llm;
        _billing = billing;
        _mapper = mapper;
    }

    public async Task<List<DTOs.ChatSessionDto>> GetSessionsAsync(string userId) { var entities = await _chatRepo.GetSessionsForUserAsync(userId); return _mapper.Map<List<DTOs.ChatSessionDto>>(entities); }

    public async Task<DTOs.ChatSessionDto> CreateSessionAsync(string userId, string? subjectId)
    {
        var session = new ChatSession
        {
            UserId = userId,
            SubjectId = string.IsNullOrEmpty(subjectId) ? null : subjectId,
            Title = "Cuộc hội thoại mới"
        };
        await _chatRepo.CreateSessionAsync(session);
        return _mapper.Map<DTOs.ChatSessionDto>(session);
    }

    public async Task<DTOs.ChatSessionDto?> GetSessionAsync(string sessionId) { var entity = await _chatRepo.GetSessionAsync(sessionId); return _mapper.Map<DTOs.ChatSessionDto>(entity); }
    public Task DeleteSessionAsync(string sessionId) => _chatRepo.DeleteSessionAsync(sessionId);
    public async Task<List<DTOs.ChatMessageDto>> GetMessagesAsync(string sessionId) { var entities = await _chatRepo.GetMessagesAsync(sessionId); return _mapper.Map<List<DTOs.ChatMessageDto>>(entities); }

    public async Task<ChatAnswer> AskAsync(string sessionId, string userId, string question)
    {
        var session = await _chatRepo.GetSessionAsync(sessionId)
            ?? throw new InvalidOperationException("Session không tồn tại");

        // Chỉ tính phí token cho sinh viên; Admin/Giảng viên dùng không giới hạn (nhưng vẫn ghi log).
        var user = await _userRepo.GetByIdAsync(userId);
        bool meter = user?.Role == "Student";
        if (meter) await _billing.EnsureFreeGrantAsync(userId);

        var history = await _chatRepo.GetMessagesAsync(sessionId);

        await _chatRepo.AddMessageAsync(new ChatMessage
        {
            SessionId = sessionId,
            Role = "user",
            Content = question
        });

        // Chặn khi sinh viên hết token — yêu cầu mua thêm gói.
        if (meter && !await _billing.HasQuotaAsync(userId))
        {
            var blocked = "⚠️ Bạn đã dùng hết token trong gói. Vui lòng mua thêm gói tại **Cửa hàng gói** để tiếp tục hỏi đáp.";
            await _chatRepo.AddMessageAsync(new ChatMessage { SessionId = sessionId, Role = "assistant", Content = blocked });
            return new ChatAnswer(blocked, new List<ChatSource>());
        }

        var searchResults = await _retrievalService.SearchAsync(question, session.SubjectId, 5);

        string answer;
        var sources = new List<ChatSource>();

        if (searchResults.Count == 0 || searchResults.All(x => x.Score < 0.2f))
        {
            answer = "Tôi không tìm thấy thông tin này trong tài liệu môn học.";
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

            var llm = await _llm.GenerateAnswerAsync(question, chunks, history);
            answer = llm.Content;

            // Ghi nhật ký token + trừ quota (nếu là sinh viên) khi thực sự gọi model thành công.
            if (!llm.IsError)
                await _billing.RecordUsageAsync(userId, sessionId, llm, "chat", meter);

            // Nếu model vẫn từ chối (ngữ cảnh không thực sự liên quan) thì KHÔNG hiển thị nguồn —
            // tránh trường hợp "không tìm thấy trong tài liệu" nhưng vẫn kèm nguồn + độ tin cậy.
            if (answer.Contains("không tìm thấy thông tin", StringComparison.OrdinalIgnoreCase))
                sources = new List<ChatSource>();
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
