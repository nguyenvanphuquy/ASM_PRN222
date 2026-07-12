using System.Diagnostics;
using System.Text.Json;
using DataAccessLayer.Entities;
using DataAccessLayer.Repositories.Interfaces;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Embeddings;
using ServiceLayer.Services.Interfaces;
using ServiceLayer.Settings;

namespace ServiceLayer.Services.Implementations;

public class EmbeddingComparisonService : IEmbeddingComparisonService
{
    private static readonly (string Id, string Display, string Provider, string Description)[] EmbeddingModels =
    {
        ("Local", "Local (hashing)", "Local", "Feature-hashing từ vựng · không cần gì · baseline luôn chạy"),
        ("multilingual-e5-base", "multilingual-e5-base", "Local ST", "intfloat · đa ngữ · chạy offline qua sentence-transformers"),
        ("PhoBERT-base", "PhoBERT-base", "Local ST", "vinai · tối ưu tiếng Việt (MLM + mean-pooling) · offline"),
        ("bge-m3", "bge-m3", "Local ST", "BAAI · đa ngữ retrieval mạnh (~2GB, chậm trên CPU) · offline"),
        ("text-embedding-3-small", "text-embedding-3-small", "OpenAI", "OpenAI (cần API key trả phí — tuỳ chọn)"),
    };

    private const int MaxDocs = 6;
    private const int MaxChunksToEmbed = 40;
    private const int TopK = 5;

    private readonly IDocumentRepository _docs;
    private readonly IChunkingFactory _chunkingFactory;
    private readonly IEmbeddingFactory _embeddingFactory;
    private readonly ISystemSettingService _settings;
    private readonly IGroqService _llm;
    private readonly IBillingService _billing;
    private readonly ISubjectService _subjects;
    private readonly IExperimentService _experiments;
    private readonly LocalStEmbeddingProvider? _localSt;

    public EmbeddingComparisonService(
        IDocumentRepository docs,
        IChunkingFactory chunkingFactory,
        IEmbeddingFactory embeddingFactory,
        ISystemSettingService settings,
        IGroqService llm,
        IBillingService billing,
        ISubjectService subjects,
        IExperimentService experiments,
        IEnumerable<IEmbeddingProvider> embeddingProviders)
    {
        _docs = docs;
        _chunkingFactory = chunkingFactory;
        _embeddingFactory = embeddingFactory;
        _settings = settings;
        _llm = llm;
        _billing = billing;
        _subjects = subjects;
        _experiments = experiments;
        _localSt = embeddingProviders.OfType<LocalStEmbeddingProvider>().FirstOrDefault();
    }

    public async Task<EmbeddingComparisonResult> CompareAsync(string question, string? subjectId, string userId)
    {
        var allSubjects = string.IsNullOrWhiteSpace(subjectId);
        string subjectName;
        if (allSubjects)
        {
            subjectId = null;
            subjectName = "Tất cả môn";
        }
        else
        {
            var subject = await _subjects.GetByIdAsync(subjectId!);
            subjectName = subject != null ? $"{subject.Code} – {subject.Name}" : subjectId!;
        }

        var chunkingName = await _settings.GetSettingAsync("Rbl.ChunkingStrategy", "SemanticKernel");
        var chunker = _chunkingFactory.GetStrategy(chunkingName);

        var docs = (allSubjects ? await _docs.GetAllAsync() : await _docs.GetBySubjectAsync(subjectId!))
            .Where(d => !string.IsNullOrWhiteSpace(d.ExtractedText))
            .OrderBy(d => d.FileName)
            .Take(MaxDocs)
            .ToList();

        var result = new EmbeddingComparisonResult
        {
            Question = question,
            SubjectId = subjectId,
            SubjectName = subjectName,
            ChunkingStrategy = chunkingName,
            DocumentsUsed = docs.Count,
        };

        if (docs.Count == 0)
        {
            result.Insights.Notes.Add("Môn học chưa có tài liệu ExtractedText. Hãy upload/index tài liệu trước.");
            return result;
        }

        // Cùng một bộ chunk cho mọi embedding model (công bằng).
        var baseChunks = new List<DocumentChunk>();
        foreach (var doc in docs)
        {
            var pages = new List<(int Page, string Text)> { (1, doc.ExtractedText!) };
            var pieces = chunker.Chunk(pages);
            for (var i = 0; i < pieces.Count; i++)
            {
                baseChunks.Add(new DocumentChunk
                {
                    Id = Guid.NewGuid().ToString(),
                    DocumentId = doc.Id,
                    SubjectId = doc.SubjectId,
                    DocumentName = doc.FileName,
                    ChunkIndex = i,
                    Content = pieces[i].Text,
                    Page = pieces[i].Page,
                });
            }
        }

        // Giới hạn số chunk embed để demo không timeout
        if (baseChunks.Count > MaxChunksToEmbed)
            baseChunks = baseChunks.Take(MaxChunksToEmbed).ToList();

        result.ChunksBuilt = baseChunks.Count;

        var openAiKey = await _settings.GetSettingAsync("Rbl.OpenAIApiKey", "");
        var sidecarUrl = await _settings.GetSettingAsync("Rbl.LocalEmbedUrl", "http://127.0.0.1:8600");
        var sidecarUp = _localSt != null
            && await RblSidecarHealth.IsOnlineAsync(_localSt.Http, sidecarUrl);

        var modelsToRun = EmbeddingModels.Where(m =>
        {
            if (m.Provider == "OpenAI" && string.IsNullOrWhiteSpace(openAiKey)) return false;
            if (m.Provider == "Local ST" && !sidecarUp) return false;
            return true;
        }).ToList();

        if (!sidecarUp && EmbeddingModels.Any(m => m.Provider == "Local ST"))
            result.Insights.Notes.Add("Sidecar embedding chưa chạy — bỏ qua e5/PhoBERT/bge. App tự khởi động sidecar khi chạy; hoặc: python tools/embedding_server.py");

        var modelResults = await Task.WhenAll(modelsToRun.Select(meta =>
            RunModelAsync(meta, question, userId, baseChunks)));
        result.Models.AddRange(modelResults);

        // Báo model bị bỏ qua (OpenAI / sidecar)
        foreach (var skipped in EmbeddingModels.Except(modelsToRun))
        {
            var reason = skipped.Provider == "OpenAI"
                ? "Chưa cấu hình OpenAI API key trong Settings."
                : "Sidecar embedding chưa sẵn sàng.";
            result.Models.Add(new EmbeddingModelResult
            {
                Model = skipped.Id,
                DisplayName = skipped.Display,
                Provider = skipped.Provider,
                Description = skipped.Description,
                IsError = true,
                ErrorMessage = reason,
                Answer = $"Bỏ qua: {reason}",
            });
        }

        result.Insights = BuildInsights(result);
        if (result.Models.Any(m => !m.IsError))
            await _experiments.SaveEmbeddingAsync(result, userId);
        return result;
    }

    private async Task<EmbeddingModelResult> RunModelAsync(
        (string Id, string Display, string Provider, string Description) meta,
        string question,
        string userId,
        List<DocumentChunk> baseChunks)
    {
        var sw = Stopwatch.StartNew();
        long embedMs = 0;
        try
        {
            var provider = _embeddingFactory.GetProvider(meta.Id)
                ?? throw new InvalidOperationException($"Không tìm thấy provider cho model {meta.Id}.");

            var embedSw = Stopwatch.StartNew();
            var queryText = PrepareText(meta.Id, question, isQuery: true);
            float[] queryVector;
            try
            {
                queryVector = await provider.GetEmbeddingAsync(queryText, meta.Id);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Embed câu hỏi thất bại ({meta.Provider}): {ex.Message}", ex);
            }

            var working = baseChunks.Select(c => new DocumentChunk
            {
                Id = c.Id,
                DocumentId = c.DocumentId,
                SubjectId = c.SubjectId,
                DocumentName = c.DocumentName,
                ChunkIndex = c.ChunkIndex,
                Content = c.Content,
                Page = c.Page,
                EmbeddingModel = meta.Id,
            }).ToList();

            var embedded = await EmbedChunksAsync(provider, meta.Id, working);
            embedSw.Stop();
            embedMs = embedSw.ElapsedMilliseconds;

            if (embedded == 0)
                throw new InvalidOperationException("Không embed được chunk nào. Kiểm tra sidecar/API key.");

            var scored = ScoreByCosine(working, queryVector, TopK);
            var topChunks = scored.Select(x => x.Chunk).ToList();
            var hits = scored.Select(x => new ChunkHit
            {
                DocumentName = x.Chunk.DocumentName,
                ChunkIndex = x.Chunk.ChunkIndex,
                Page = x.Chunk.Page,
                Score = x.Score,
                Snippet = Truncate(x.Chunk.Content, 180),
            }).ToList();

            LlmResult llm;
            string answer;
            if (scored.Count == 0 || scored.All(x => x.Score < 0.2f))
            {
                answer = "Tôi không tìm thấy thông tin này trong tài liệu môn học.";
                llm = new LlmResult(answer, "", 0, 0, 0, 0);
            }
            else
            {
                llm = await _llm.GenerateAnswerAsync(question, topChunks, Array.Empty<ChatMessage>());
                answer = llm.Content;
                if (!llm.IsError && llm.TotalTokens > 0)
                    await _billing.RecordUsageAsync(userId, null, llm, "embed-compare", meter: false);
            }

            sw.Stop();
            return new EmbeddingModelResult
            {
                Model = meta.Id,
                DisplayName = meta.Display,
                Provider = meta.Provider,
                Description = meta.Description,
                EmbeddedChunks = embedded,
                RetrievedCount = scored.Count,
                TopScore = scored.Count > 0 ? scored.Max(x => x.Score) : 0,
                AvgTopScore = scored.Count > 0 ? scored.Average(x => x.Score) : 0,
                Answer = answer,
                Hits = hits,
                PromptTokens = llm.PromptTokens,
                CompletionTokens = llm.CompletionTokens,
                TotalTokens = llm.TotalTokens,
                LatencyMs = sw.ElapsedMilliseconds,
                EmbedLatencyMs = embedMs,
                CostUsd = string.IsNullOrEmpty(llm.Model)
                    ? 0
                    : ModelCatalog.EstimateCostUsd(llm.Model, llm.PromptTokens, llm.CompletionTokens),
                IsError = llm.IsError,
                ErrorMessage = llm.IsError ? llm.Content : null,
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new EmbeddingModelResult
            {
                Model = meta.Id,
                DisplayName = meta.Display,
                Provider = meta.Provider,
                Description = meta.Description,
                LatencyMs = sw.ElapsedMilliseconds,
                EmbedLatencyMs = embedMs,
                IsError = true,
                ErrorMessage = ex.Message,
                Answer = $"Không chạy được: {ex.Message}",
            };
        }
    }

    private async Task<int> EmbedChunksAsync(IEmbeddingProvider provider, string modelId, List<DocumentChunk> working)
    {
        // Batch qua sidecar — nhanh hơn nhiều so với gọi tuần tự từng chunk.
        if (provider is LocalStEmbeddingProvider localSt)
        {
            var passages = working.Select(c => PrepareText(modelId, c.Content, isQuery: false)).ToList();
            var vectors = await localSt.GetEmbeddingsBatchAsync(passages, modelId);
            var count = Math.Min(vectors.Count, working.Count);
            for (var i = 0; i < count; i++)
                working[i].VectorJson = JsonSerializer.Serialize(vectors[i]);
            return count;
        }

        var sem = new SemaphoreSlim(4);
        var tasks = working.Select(async c =>
        {
            await sem.WaitAsync();
            try
            {
                var passage = PrepareText(modelId, c.Content, isQuery: false);
                var vec = await provider.GetEmbeddingAsync(passage, modelId);
                c.VectorJson = JsonSerializer.Serialize(vec);
            }
            catch { /* bỏ chunk lỗi */ }
            finally { sem.Release(); }
        });
        await Task.WhenAll(tasks);
        return working.Count(c => !string.IsNullOrEmpty(c.VectorJson));
    }

    private static EmbeddingComparisonInsights BuildInsights(EmbeddingComparisonResult result)
    {
        var insights = new EmbeddingComparisonInsights();
        var ok = result.Models.Where(m => !m.IsError).ToList();
        if (ok.Count == 0)
        {
            insights.Notes.Add("Không có embedding model nào chạy thành công. Kiểm tra OpenAI key / HuggingFace token trong Settings.");
            return insights;
        }

        var bestRetrieval = ok.OrderByDescending(m => m.AvgTopScore).ThenByDescending(m => m.TopScore).First();
        var bestEmbed = ok.OrderBy(m => m.EmbedLatencyMs).First();
        var bestE2E = ok.OrderBy(m => m.LatencyMs).First();

        insights.WinnerRetrieval = bestRetrieval.DisplayName;
        insights.WinnerEmbedLatency = bestEmbed.DisplayName;
        insights.WinnerE2ELatency = bestE2E.DisplayName;

        insights.Notes.Add($"Cùng {result.ChunksBuilt} chunk (strategy: {result.ChunkingStrategy}) · {result.DocumentsUsed} tài liệu.");
        insights.Notes.Add($"Retrieval tốt nhất: {bestRetrieval.DisplayName} (avg cosine {bestRetrieval.AvgTopScore:F3}).");
        insights.Notes.Add($"Embed nhanh nhất: {bestEmbed.DisplayName} ({bestEmbed.EmbedLatencyMs} ms).");
        insights.Notes.Add("Benchmark in-memory — không ghi đè vector index của chatbot.");
        if (ok.Any(m => m.Model == "PhoBERT-base"))
            insights.Notes.Add("PhoBERT-base là MLM; điểm cosine có thể thấp hơn các model embedding chuyên dụng.");

        var failed = result.Models.Where(m => m.IsError).ToList();
        if (failed.Count > 0)
            insights.Notes.Add($"Model lỗi/bỏ qua: {string.Join(", ", failed.Select(f => f.DisplayName))}.");

        return insights;
    }

    /// <summary>e5 khuyến nghị prefix query:/passage: để retrieval tốt hơn.</summary>
    private static string PrepareText(string modelId, string text, bool isQuery)
    {
        if (modelId.Contains("e5", StringComparison.OrdinalIgnoreCase))
            return isQuery ? $"query: {text}" : $"passage: {text}";
        return text;
    }

    private static List<(DocumentChunk Chunk, float Score)> ScoreByCosine(
        List<DocumentChunk> chunks, float[] queryVector, int limit)
    {
        return chunks
            .Select(c =>
            {
                if (string.IsNullOrEmpty(c.VectorJson)) return (Chunk: c, Score: -1f);
                try
                {
                    var vec = JsonSerializer.Deserialize<float[]>(c.VectorJson);
                    if (vec == null || vec.Length != queryVector.Length) return (Chunk: c, Score: -1f);
                    return (Chunk: c, Score: Cosine(queryVector, vec));
                }
                catch { return (Chunk: c, Score: -1f); }
            })
            .Where(x => x.Score > 0.15f)
            .OrderByDescending(x => x.Score)
            .Take(limit)
            .ToList();
    }

    private static float Cosine(float[] a, float[] b)
    {
        float dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        return (na == 0 || nb == 0) ? 0 : dot / (float)(Math.Sqrt(na) * Math.Sqrt(nb));
    }

    private static string Truncate(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? "";
        return text[..max].TrimEnd() + "…";
    }
}
