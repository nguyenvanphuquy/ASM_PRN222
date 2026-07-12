using ServiceLayer.Dtos;

namespace ServiceLayer.Services.Interfaces;

public interface IExperimentService
{
    Task SaveChunkingAsync(ChunkingComparisonResult result, string userId);
    Task SaveEmbeddingAsync(EmbeddingComparisonResult result, string userId);
    Task SaveRagVsFineTunedAsync(RagVsFineTunedResult result, string userId);
    Task<ExperimentDashboardDto> GetDashboardAsync(int recentTake = 30, string? filterKind = null);
}
