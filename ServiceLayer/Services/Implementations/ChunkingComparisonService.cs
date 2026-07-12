using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using DataAccessLayer.Entities;
using DataAccessLayer.Repositories.Interfaces;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Embeddings;
using ServiceLayer.Services.Interfaces;
using ServiceLayer.Settings;

namespace ServiceLayer.Services.Implementations;

public class ChunkingComparisonService : IChunkingComparisonService
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "tom", "tat", "cho", "toi", "tai", "lieu", "cua", "mon", "gium", "giup",
        "hay", "nhe", "voi", "nay", "minh", "ban", "oi", "duoc", "the", "nao",
        "lai", "di", "va", "cac", "mot", "nhung", "trong", "ve", "la", "co",
        "khong", "hoac", "thi", "se", "day", "dum", "list", "ke", "ra", "noi",
        "summarize", "summary", "about", "the", "please", "give", "show"
    };

    private static readonly (string Name, string Display, string Description)[] StrategyMeta =
    {
        ("SemanticKernel", "Semantic (SK)", "Cắt theo đoạn ngữ nghĩa (Semantic Kernel TextChunker)"),
        ("FixedSize", "Fixed Size", "Cửa sổ cố định 500 ký tự, overlap 50"),
        ("Sentence", "Sentence", "Cắt theo câu (. ! ?), gộp câu ngắn"),
    };

    private readonly IDocumentRepository _docs;
    private readonly IChunkingFactory _chunkingFactory;
    private readonly IEnumerable<IChunkingStrategy> _strategies;
    private readonly IEmbeddingFactory _embeddingFactory;
    private readonly ISystemSettingService _settings;
    private readonly IGroqService _llm;
    private readonly IBillingService _billing;
    private readonly ISubjectService _subjects;

    public ChunkingComparisonService(
        IDocumentRepository docs,
        IChunkingFactory chunkingFactory,
        IEnumerable<IChunkingStrategy> strategies,
        IEmbeddingFactory embeddingFactory,
        ISystemSettingService settings,
        IGroqService llm,
        IBillingService billing,
        ISubjectService subjects)
    {
        _docs = docs;
        _chunkingFactory = chunkingFactory;
        _strategies = strategies;
        _embeddingFactory = embeddingFactory;
        _settings = settings;
        _llm = llm;
        _billing = billing;
        _subjects = subjects;
    }

    public async Task<ChunkingComparisonResult> CompareAsync(string question, string subjectId, string userId)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
            throw new ArgumentException("Cần chọn môn học để benchmark chunking.", nameof(subjectId));

        var subject = await _subjects.GetByIdAsync(subjectId);
        var subjectName = subject != null ? $"{subject.Code} – {subject.Name}" : subjectId;

        var docs = (await _docs.GetBySubjectAsync(subjectId))
            .Where(d => !string.IsNullOrWhiteSpace(d.ExtractedText))
            .OrderBy(d => d.FileName)
            .Take(8) // giới hạn để benchmark chạy nhanh
            .ToList();

        var result = new ChunkingComparisonResult
        {
            Question = question,
            SubjectId = subjectId,
            SubjectName = subjectName,
            DocumentsUsed = docs.Count,
        };

        if (docs.Count == 0)
        {
            result.Insights.Notes.Add("Môn học chưa có tài liệu ExtractedText. Hãy upload/index tài liệu trước.");
            return result;
        }

        var pagesByDoc = docs
            .Select(d => (Doc: d, Pages: SplitPages(d.ExtractedText!)))
            .Where(x => x.Pages.Count > 0)
            .ToList();

        var embeddingModel = await _settings.GetSettingAsync("Rbl.EmbeddingModel", "Keyword");
        result.EmbeddingMode = embeddingModel;
        var embedder = _embeddingFactory.GetProvider(embeddingModel);

        float[]? queryVector = null;
        if (embedder != null)
        {
            try { queryVector = await embedder.GetEmbeddingAsync(question, embeddingModel); }
            catch { /* fallback keyword */ }
        }

        var strategyNames = StrategyMeta
            .Select(m => m.Name)
            .Where(n => _strategies.Any(s => string.Equals(s.Name, n, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (strategyNames.Count == 0)
            strategyNames = _strategies.Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var strategyName in strategyNames)
        {
            var meta = StrategyMeta.FirstOrDefault(m =>
                string.Equals(m.Name, strategyName, StringComparison.OrdinalIgnoreCase));
            var display = string.IsNullOrEmpty(meta.Display) ? strategyName : meta.Display;
            var description = string.IsNullOrEmpty(meta.Description) ? strategyName : meta.Description;

            var sw = Stopwatch.StartNew();
            try
            {
                var chunker = _chunkingFactory.GetStrategy(strategyName);
                var chunks = new List<DocumentChunk>();
                foreach (var (doc, pages) in pagesByDoc)
                {
                    var pieces = chunker.Chunk(pages);
                    for (var i = 0; i < pieces.Count; i++)
                    {
                        chunks.Add(new DocumentChunk
                        {
                            Id = Guid.NewGuid().ToString(),
                            DocumentId = doc.Id,
                            SubjectId = subjectId,
                            DocumentName = doc.FileName,
                            ChunkIndex = i,
                            Content = pieces[i].Text,
                            Page = pieces[i].Page,
                            EmbeddingModel = embeddingModel,
                        });
                    }
                }

                // Embed tối đa 120 chunk / strategy để tránh timeout API khi demo
                if (embedder != null && queryVector != null)
                {
                    var toEmbed = chunks.Take(120).ToList();
                    foreach (var c in toEmbed)
                    {
                        try
                        {
                            var vec = await embedder.GetEmbeddingAsync(c.Content, embeddingModel);
                            c.VectorJson = JsonSerializer.Serialize(vec);
                        }
                        catch { /* keep null → keyword */ }
                    }
                }

                var scored = ScoreChunks(question, chunks, queryVector, limit: 5);
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
                        await _billing.RecordUsageAsync(userId, null, llm, "chunk-compare", meter: false);
                }

                sw.Stop();
                var avgLen = chunks.Count == 0 ? 0 : (int)chunks.Average(c => c.Content.Length);

                result.Strategies.Add(new ChunkingStrategyResult
                {
                    Strategy = strategyName,
                    DisplayName = display,
                    Description = description,
                    TotalChunks = chunks.Count,
                    AvgChunkLength = avgLen,
                    RetrievedCount = scored.Count,
                    TopScore = scored.Count > 0 ? scored.Max(x => x.Score) : 0,
                    AvgTopScore = scored.Count > 0 ? scored.Average(x => x.Score) : 0,
                    Answer = answer,
                    Hits = hits,
                    PromptTokens = llm.PromptTokens,
                    CompletionTokens = llm.CompletionTokens,
                    TotalTokens = llm.TotalTokens,
                    LatencyMs = sw.ElapsedMilliseconds,
                    CostUsd = string.IsNullOrEmpty(llm.Model)
                        ? 0
                        : ModelCatalog.EstimateCostUsd(llm.Model, llm.PromptTokens, llm.CompletionTokens),
                    IsError = llm.IsError,
                    ErrorMessage = llm.IsError ? llm.Content : null,
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.Strategies.Add(new ChunkingStrategyResult
                {
                    Strategy = strategyName,
                    DisplayName = display,
                    Description = description,
                    LatencyMs = sw.ElapsedMilliseconds,
                    IsError = true,
                    ErrorMessage = ex.Message,
                    Answer = $"Lỗi: {ex.Message}",
                });
            }
        }

        result.Insights = BuildInsights(result);
        return result;
    }

    private static ChunkingComparisonInsights BuildInsights(ChunkingComparisonResult result)
    {
        var insights = new ChunkingComparisonInsights();
        var ok = result.Strategies.Where(s => !s.IsError).ToList();
        if (ok.Count == 0)
        {
            insights.Notes.Add("Không có strategy nào chạy thành công.");
            return insights;
        }

        var bestRetrieval = ok.OrderByDescending(s => s.AvgTopScore).ThenByDescending(s => s.TopScore).First();
        var bestLatency = ok.OrderBy(s => s.LatencyMs).First();
        var densest = ok.OrderBy(s => s.AvgChunkLength).First(); // chunk ngắn hơn = mật độ cắt cao hơn

        insights.WinnerRetrieval = bestRetrieval.DisplayName;
        insights.WinnerLatency = bestLatency.DisplayName;
        insights.WinnerChunkDensity = densest.DisplayName;

        insights.Notes.Add($"Dùng {result.DocumentsUsed} tài liệu · chế độ embedding: {result.EmbeddingMode}.");
        insights.Notes.Add($"Retrieval tốt nhất: {bestRetrieval.DisplayName} (avg score {bestRetrieval.AvgTopScore:F3}).");
        insights.Notes.Add($"Nhanh nhất end-to-end: {bestLatency.DisplayName} ({bestLatency.LatencyMs} ms).");
        insights.Notes.Add("Benchmark chạy in-memory — không ghi đè chunk đang dùng cho chatbot.");
        if (result.EmbeddingMode.Equals("Keyword", StringComparison.OrdinalIgnoreCase))
            insights.Notes.Add("Đang Keyword search. Bật embedding trong Settings để so cosine similarity.");

        return insights;
    }

    private static List<(int Page, string Text)> SplitPages(string extractedText)
    {
        // ExtractedText thường nối bằng \n\n; giữ 1 page nếu không có marker rõ.
        if (string.IsNullOrWhiteSpace(extractedText)) return [];
        return [(1, extractedText)];
    }

    private static List<(DocumentChunk Chunk, float Score)> ScoreChunks(
        string query, List<DocumentChunk> chunks, float[]? queryVector, int limit)
    {
        if (chunks.Count == 0) return [];

        // Vector cosine trước
        if (queryVector != null && queryVector.Length > 0)
        {
            var vectorScored = chunks
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
                .Where(x => x.Score > 0.3f)
                .OrderByDescending(x => x.Score)
                .Take(limit)
                .ToList();
            if (vectorScored.Count > 0) return vectorScored;
        }

        // Keyword fallback
        var keywords = query
            .Split([' ', ',', '.', '?', '!', ':', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim())
            .Where(w => w.Length >= 3 && !StopWords.Contains(RemoveDiacritics(w)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keywords.Count == 0)
            return chunks.Take(limit).Select(c => (c, 0.1f)).ToList();

        int totalKw = keywords.Count;
        return chunks
            .Select(c =>
            {
                var content = c.Content ?? "";
                var name = c.DocumentName ?? "";
                int hit = keywords.Count(kw =>
                    content.Contains(kw, StringComparison.OrdinalIgnoreCase)
                    || name.Contains(kw, StringComparison.OrdinalIgnoreCase));
                if (hit == 0) return (Chunk: c, Score: 0f);
                float coverage = (float)hit / totalKw;
                int tf = keywords.Sum(kw => CountOccurrences(content, kw));
                float tfBonus = Math.Min(tf / 20f, 1f) * 0.15f;
                float score = Math.Clamp(coverage * 0.85f + tfBonus, 0.1f, 1f);
                return (Chunk: c, Score: score);
            })
            .Where(x => x.Score > 0)
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

    private static int CountOccurrences(string text, string keyword)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword)) return 0;
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(keyword, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            idx += keyword.Length;
        }
        return count;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string Truncate(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? "";
        return text[..max].TrimEnd() + "…";
    }
}
