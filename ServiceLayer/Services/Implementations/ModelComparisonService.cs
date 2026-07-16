using DataAccessLayer.Entities;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;
using ServiceLayer.Settings;

namespace ServiceLayer.Services.Implementations;

public class ModelComparisonService : IModelComparisonService
{
    private readonly IRetrievalService _retrieval;
    private readonly ICerebrasService _llm;
    private readonly IBillingService _billing;

    public ModelComparisonService(IRetrievalService retrieval, ICerebrasService llm, IBillingService billing)
    {
        _retrieval = retrieval;
        _llm = llm;
        _billing = billing;
    }

    public async Task<ModelComparisonResult> CompareAsync(string question, string? subjectId, string userId)
    {
        // Cùng một ngữ cảnh RAG cho mọi model để so sánh công bằng.
        var search = await _retrieval.SearchAsync(question, subjectId, 5);
        var chunks = search.Select(x => x.Chunk).ToList();
        IReadOnlyList<ChatMessage> history = Array.Empty<ChatMessage>();

        // Gọi song song các model (HttpClient an toàn cho gọi đồng thời).
        var tasks = ModelCatalog.All
            .Select(m => _llm.GenerateAnswerWithModelAsync(m.Id, question, chunks, history))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        var result = new ModelComparisonResult
        {
            Question = question,
            ContextChunks = chunks.Count,
        };

        foreach (var r in results)
        {
            var info = ModelCatalog.Get(r.Model);
            result.Answers.Add(new ModelAnswer
            {
                Model = r.Model,
                DisplayName = info.DisplayName,
                Vendor = info.Vendor,
                Answer = r.Content,
                PromptTokens = r.PromptTokens,
                CompletionTokens = r.CompletionTokens,
                TotalTokens = r.TotalTokens,
                LatencyMs = r.LatencyMs,
                CostUsd = ModelCatalog.EstimateCostUsd(r.Model, r.PromptTokens, r.CompletionTokens),
                IsError = r.IsError,
            });
        }

        // Ghi nhật ký token TUẦN TỰ sau khi gọi xong (DbContext không an toàn đa luồng).
        // Không trừ quota (đây là công cụ benchmark của admin/giảng viên).
        foreach (var r in results.Where(r => !r.IsError && r.TotalTokens > 0))
            await _billing.RecordUsageAsync(userId, null, r, "compare", meter: false);

        return result;
    }
}
