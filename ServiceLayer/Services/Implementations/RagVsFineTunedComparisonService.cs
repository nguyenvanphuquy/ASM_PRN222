using DataAccessLayer.Entities;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;
using ServiceLayer.Settings;

namespace ServiceLayer.Services.Implementations;

public class RagVsFineTunedComparisonService : IRagVsFineTunedComparisonService
{
    private readonly IRetrievalService _retrieval;
    private readonly ICerebrasService _llm;
    private readonly IBillingService _billing;
    private readonly ISubjectService _subjects;
    private readonly IExperimentService _experiments;

    public RagVsFineTunedComparisonService(
        IRetrievalService retrieval,
        ICerebrasService llm,
        IBillingService billing,
        ISubjectService subjects,
        IExperimentService experiments)
    {
        _retrieval = retrieval;
        _llm = llm;
        _billing = billing;
        _subjects = subjects;
        _experiments = experiments;
    }

    public async Task<RagVsFineTunedResult> CompareAsync(string question, string? subjectId, string userId)
    {
        string? subjectName = null;
        if (!string.IsNullOrEmpty(subjectId))
        {
            var subject = await _subjects.GetByIdAsync(subjectId);
            subjectName = subject != null ? $"{subject.Code} – {subject.Name}" : null;
        }

        // --- RAG path ---
        var search = await _retrieval.SearchAsync(question, subjectId, 5);
        var chunks = search.Select(x => x.Chunk).ToList();
        var sources = BuildSources(search);

        // --- RAG + Fine-tuned: chạy song song sau retrieval ---
        LlmResult ragLlm;
        string ragAnswer;
        bool ragGrounded;
        LlmResult ftLlm;

        if (search.Count == 0 || search.All(x => x.Score < 0.2f))
        {
            ragAnswer = "Tôi không tìm thấy thông tin này trong tài liệu môn học.";
            ragLlm = new LlmResult(ragAnswer, "", 0, 0, 0, 0);
            ragGrounded = true;
            sources = [];
            ftLlm = await _llm.GenerateParametricAnswerAsync(question, subjectName);
        }
        else
        {
            var ftTask = _llm.GenerateParametricAnswerAsync(question, subjectName);
            var ragTask = _llm.GenerateAnswerAsync(question, chunks, Array.Empty<ChatMessage>());
            await Task.WhenAll(ftTask, ragTask);
            ftLlm = await ftTask;
            ragLlm = await ragTask;
            ragAnswer = ragLlm.Content;
            ragGrounded = true;
            if (ragAnswer.Contains("không tìm thấy thông tin", StringComparison.OrdinalIgnoreCase))
                sources = [];
        }

        var ftAnswer = ftLlm.Content;

        var modelId = !string.IsNullOrEmpty(ragLlm.Model) ? ragLlm.Model
            : (!string.IsNullOrEmpty(ftLlm.Model) ? ftLlm.Model : ModelCatalog.All[0].Id);

        var result = new RagVsFineTunedResult
        {
            Question = question,
            SubjectId = subjectId,
            SubjectName = subjectName,
            Model = modelId,
            ContextChunks = chunks.Count,
            Rag = new ApproachAnswer
            {
                Approach = "RAG",
                Label = "RAG (Retrieval-Augmented Generation)",
                Description = "Truy xuất chunk tài liệu → đưa vào prompt → trả lời có trích dẫn nguồn.",
                Answer = ragAnswer,
                Sources = sources,
                PromptTokens = ragLlm.PromptTokens,
                CompletionTokens = ragLlm.CompletionTokens,
                TotalTokens = ragLlm.TotalTokens,
                LatencyMs = ragLlm.LatencyMs,
                CostUsd = ModelCatalog.EstimateCostUsd(modelId, ragLlm.PromptTokens, ragLlm.CompletionTokens),
                IsError = ragLlm.IsError,
                GroundedInDocuments = (ragGrounded && sources.Count > 0)
                    || ragAnswer.Contains("không tìm thấy thông tin", StringComparison.OrdinalIgnoreCase)
            },
            FineTuned = new ApproachAnswer
            {
                Approach = "FineTuned",
                Label = "Fine-tuned / Parametric (mô phỏng)",
                Description = "Cùng base LLM, không retrieval — mô phỏng model đã fine-tune trả lời từ kiến thức nội tại.",
                Answer = ftAnswer,
                Sources = [],
                PromptTokens = ftLlm.PromptTokens,
                CompletionTokens = ftLlm.CompletionTokens,
                TotalTokens = ftLlm.TotalTokens,
                LatencyMs = ftLlm.LatencyMs,
                CostUsd = ModelCatalog.EstimateCostUsd(modelId, ftLlm.PromptTokens, ftLlm.CompletionTokens),
                IsError = ftLlm.IsError,
                GroundedInDocuments = false
            }
        };

        result.Insights = BuildInsights(result);

        // Log token (không trừ quota — công cụ RBL).
        foreach (var r in new[] { ragLlm, ftLlm }.Where(r => !r.IsError && r.TotalTokens > 0))
            await _billing.RecordUsageAsync(userId, null, r, "rag-vs-ft", meter: false);

        await _experiments.SaveRagVsFineTunedAsync(result, userId);
        return result;
    }

    private static List<ChatSource> BuildSources(List<(DocumentChunk Chunk, float Score)> search)
    {
        return search
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
                    Snippet = best.Chunk.Content.Length > 300
                        ? best.Chunk.Content[..300] + "..."
                        : best.Chunk.Content,
                    ConfidenceScore = Math.Min(best.Score, 1.0f)
                };
            })
            .OrderByDescending(s => s.ConfidenceScore)
            .ToList();
    }

    private static ComparisonInsights BuildInsights(RagVsFineTunedResult result)
    {
        var insights = new ComparisonInsights();
        var rag = result.Rag;
        var ft = result.FineTuned;

        if (!rag.IsError && !ft.IsError)
        {
            insights.WinnerLatency = rag.LatencyMs <= ft.LatencyMs ? "RAG" : "Fine-tuned";
            insights.WinnerTokens = rag.TotalTokens <= ft.TotalTokens ? "RAG" : "Fine-tuned";
        }

        insights.WinnerGroundedness = rag.HasCitations || rag.GroundedInDocuments
            ? "RAG"
            : "Không rõ";

        insights.Notes.Add("RAG gắn câu trả lời với tài liệu đã index → giảm hallucination, có citation.");
        insights.Notes.Add("Fine-tuned (parametric) trả lời nhanh từ kiến thức nội tại nhưng không chứng minh nguồn trong corpus.");
        insights.Notes.Add("Demo dùng cùng base LLM Cerebras: nhánh Fine-tuned = prompt chuyên gia + KHÔNG retrieval (mô phỏng hành vi model fine-tune).");
        if (result.ContextChunks == 0)
            insights.Notes.Add("⚠ Chưa có chunk liên quan — RAG sẽ từ chối trả lời; Fine-tuned vẫn có thể bịa nội dung.");
        if (rag.HasCitations && !ft.HasCitations)
            insights.Notes.Add("✓ Chỉ RAG cung cấp trích dẫn nguồn tài liệu gốc.");

        return insights;
    }
}
