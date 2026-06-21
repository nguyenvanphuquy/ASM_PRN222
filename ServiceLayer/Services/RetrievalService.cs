using DataAccessLayer.Entities;
using DataAccessLayer.Repositories;
using ServiceLayer.Services.Embeddings;

namespace ServiceLayer.Services;

public class RetrievalService : IRetrievalService
{
    private readonly IDocumentChunkRepository _chunkRepo;
    private readonly IEmbeddingFactory _embeddingFactory;
    private readonly ISystemSettingService _settingService;

    public RetrievalService(
        IDocumentChunkRepository chunkRepo,
        IEmbeddingFactory embeddingFactory,
        ISystemSettingService settingService)
    {
        _chunkRepo = chunkRepo;
        _embeddingFactory = embeddingFactory;
        _settingService = settingService;
    }

    public async Task<List<(DocumentChunk Chunk, float Score)>> SearchAsync(string query, string? subjectId, int limit)
    {
        var embeddingModel = await _settingService.GetSettingAsync("Rbl.EmbeddingModel", "Keyword");
        var embedder = _embeddingFactory.GetProvider(embeddingModel);
        float[]? queryVector = null;

        if (embedder != null)
        {
            try
            {
                queryVector = await embedder.GetEmbeddingAsync(query, embeddingModel);
            }
            catch { /* Fallback to null */ }
        }

        return await _chunkRepo.SearchAsync(query, subjectId, limit, queryVector);
    }
}


