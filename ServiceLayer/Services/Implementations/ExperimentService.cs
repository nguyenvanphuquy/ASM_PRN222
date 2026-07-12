using System.Text.Json;
using DataAccessLayer.Entities;
using DataAccessLayer.Repositories.Interfaces;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Embeddings;
using ServiceLayer.Services.Interfaces;

namespace ServiceLayer.Services.Implementations;

public class ExperimentService : IExperimentService
{
    public const string KindChunking = "chunking";
    public const string KindEmbedding = "embedding";
    public const string KindRagVsFt = "rag-vs-ft";

    private readonly IExperimentRepository _repo;
    private readonly ISystemSettingService _settings;
    private readonly LocalStEmbeddingProvider? _localSt;

    public ExperimentService(
        IExperimentRepository repo,
        ISystemSettingService settings,
        IEnumerable<IEmbeddingProvider> embeddingProviders)
    {
        _repo = repo;
        _settings = settings;
        _localSt = embeddingProviders.OfType<LocalStEmbeddingProvider>().FirstOrDefault();
    }

    public async Task SaveChunkingAsync(ChunkingComparisonResult result, string userId)
    {
        var run = new ExperimentRun
        {
            Kind = KindChunking,
            Question = Truncate(result.Question, 1000),
            SubjectId = result.SubjectId,
            SubjectName = result.SubjectName,
            UserId = userId,
            WinnerLabel = result.Insights.WinnerRetrieval,
            NotesJson = JsonSerializer.Serialize(result.Insights.Notes),
            Variants = result.Strategies.Select(s => new ExperimentVariant
            {
                VariantKey = s.Strategy,
                VariantLabel = s.DisplayName,
                Score = s.AvgTopScore,
                LatencyMs = s.LatencyMs,
                TotalTokens = s.TotalTokens,
                ExtraInt = s.TotalChunks,
                IsError = s.IsError,
                AnswerPreview = Truncate(s.Answer, 500),
            }).ToList()
        };
        await _repo.AddAsync(run);
    }

    public async Task SaveEmbeddingAsync(EmbeddingComparisonResult result, string userId)
    {
        var run = new ExperimentRun
        {
            Kind = KindEmbedding,
            Question = Truncate(result.Question, 1000),
            SubjectId = result.SubjectId,
            SubjectName = result.SubjectName,
            UserId = userId,
            WinnerLabel = result.Insights.WinnerRetrieval,
            NotesJson = JsonSerializer.Serialize(result.Insights.Notes),
            Variants = result.Models.Select(m => new ExperimentVariant
            {
                VariantKey = m.Model,
                VariantLabel = m.DisplayName,
                Score = m.AvgTopScore,
                LatencyMs = m.LatencyMs,
                TotalTokens = m.TotalTokens,
                ExtraInt = m.EmbeddedChunks,
                IsError = m.IsError,
                AnswerPreview = Truncate(m.Answer, 500),
            }).ToList()
        };
        await _repo.AddAsync(run);
    }

    public async Task SaveRagVsFineTunedAsync(RagVsFineTunedResult result, string userId)
    {
        var winner = result.Insights.WinnerGroundedness;
        if (string.IsNullOrWhiteSpace(winner) || winner == "Không rõ")
            winner = result.Insights.WinnerLatency;

        var run = new ExperimentRun
        {
            Kind = KindRagVsFt,
            Question = Truncate(result.Question, 1000),
            SubjectId = result.SubjectId,
            SubjectName = result.SubjectName,
            UserId = userId,
            WinnerLabel = winner,
            NotesJson = JsonSerializer.Serialize(result.Insights.Notes),
            Variants =
            [
                ToVariant("RAG", result.Rag.Label, result.Rag),
                ToVariant("FineTuned", result.FineTuned.Label, result.FineTuned),
            ]
        };
        await _repo.AddAsync(run);
    }

    public async Task<ExperimentDashboardDto> GetDashboardAsync(int recentTake = 30, string? filterKind = null)
    {
        var counts = await _repo.GetCountsByKindAsync();
        counts.TryGetValue(KindChunking, out var chunkingRuns);
        counts.TryGetValue(KindEmbedding, out var embeddingRuns);
        counts.TryGetValue(KindRagVsFt, out var ragRuns);

        var sidecarUrl = await _settings.GetSettingAsync("Rbl.LocalEmbedUrl", "http://127.0.0.1:8600");
        var sidecarOnline = _localSt != null
            && await RblSidecarHealth.IsOnlineAsync(_localSt.Http, sidecarUrl);

        var recent = await _repo.GetRecentAsync(recentTake, filterKind);
        var since = DateTime.UtcNow.AddDays(-90);
        var variants = await _repo.GetVariantsSinceAsync(since);

        return new ExperimentDashboardDto
        {
            TotalRuns = counts.Values.Sum(),
            ChunkingRuns = chunkingRuns,
            EmbeddingRuns = embeddingRuns,
            RagVsFtRuns = ragRuns,
            SidecarOnline = sidecarOnline,
            FilterKind = filterKind,
            Ragas = RagasResultsLoader.TryLoad() ?? new RagasSummaryDto(),
            RecentRuns = recent.Select(MapRun).ToList(),
            ChunkingAgg = Aggregate(variants, KindChunking),
            EmbeddingAgg = Aggregate(variants, KindEmbedding),
            RagVsFtAgg = Aggregate(variants, KindRagVsFt),
        };
    }

    private static ExperimentVariant ToVariant(string key, string label, ApproachAnswer a)
        => new()
        {
            VariantKey = key,
            VariantLabel = string.IsNullOrWhiteSpace(label) ? key : label,
            Score = a.Sources.Count > 0 ? a.Sources.Max(s => s.ConfidenceScore) : (a.GroundedInDocuments ? 1f : 0f),
            LatencyMs = a.LatencyMs,
            TotalTokens = a.TotalTokens,
            ExtraInt = a.Sources.Count,
            IsError = a.IsError,
            AnswerPreview = Truncate(a.Answer, 500),
        };

    private static List<VariantAggDto> Aggregate(List<ExperimentVariant> all, string kind)
        => all
            .Where(v => v.Run?.Kind == kind)
            .GroupBy(v => v.VariantLabel)
            .Select(g => new VariantAggDto
            {
                VariantLabel = g.Key,
                AvgScore = g.Average(x => (double)x.Score),
                AvgLatencyMs = g.Average(x => (double)x.LatencyMs),
                Runs = g.Count(),
            })
            .OrderByDescending(x => x.AvgScore)
            .ToList();

    private static ExperimentRunDto MapRun(ExperimentRun r)
        => new()
        {
            Id = r.Id,
            Kind = r.Kind,
            KindLabel = r.Kind switch
            {
                KindChunking => "Chunking",
                KindEmbedding => "Embedding",
                KindRagVsFt => "RAG vs Fine-tuned",
                _ => r.Kind
            },
            Question = r.Question,
            SubjectName = r.SubjectName,
            WinnerLabel = r.WinnerLabel,
            CreatedAt = r.CreatedAt,
            Variants = r.Variants.Select(v => new ExperimentVariantDto
            {
                VariantKey = v.VariantKey,
                VariantLabel = v.VariantLabel,
                Score = v.Score,
                LatencyMs = v.LatencyMs,
                TotalTokens = v.TotalTokens,
                ExtraInt = v.ExtraInt,
                IsError = v.IsError,
                AnswerPreview = v.AnswerPreview,
            }).ToList()
        };

    private static string Truncate(string? text, int max)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= max ? text : text[..max];
    }
}
