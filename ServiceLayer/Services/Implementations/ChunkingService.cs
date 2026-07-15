using System.Text.Json;
using DataAccessLayer.Entities;
using DataAccessLayer.Repositories.Interfaces;
using ServiceLayer.Services.Embeddings;
using ServiceLayer.Services.Interfaces;

namespace ServiceLayer.Services.Implementations;

public class ChunkingService : IChunkingService
{
    private readonly IDocumentChunkRepository _chunkRepo;
    private readonly IChunkingFactory _chunkingFactory;
    private readonly IEmbeddingFactory _embeddingFactory;
    private readonly ISystemSettingService _settingService;

    public ChunkingService(
        IDocumentChunkRepository chunkRepo,
        IChunkingFactory chunkingFactory,
        IEmbeddingFactory embeddingFactory,
        ISystemSettingService settingService)
    {
        _chunkRepo = chunkRepo;
        _chunkingFactory = chunkingFactory;
        _embeddingFactory = embeddingFactory;
        _settingService = settingService;
    }

    public Task<int> ChunkAndSaveAsync(string documentId, string subjectId, string fileName, string extractedText)
    {
        if (string.IsNullOrWhiteSpace(extractedText))
            return Task.FromResult(0);

        var pages = new List<(int Page, string Text)> { (1, extractedText) };
        return ChunkAndSaveAsync(documentId, subjectId, fileName, pages);
    }

    public async Task<int> ChunkAndSaveAsync(
        string documentId,
        string subjectId,
        string fileName,
        IReadOnlyList<(int Page, string Text)> pages)
    {
        var nonEmpty = pages
            .Select(p => (p.Page, Text: TextExtractor.NormalizeText(p.Text)))
            .Where(p => !string.IsNullOrWhiteSpace(p.Text))
            .ToList();

        if (nonEmpty.Count == 0) return 0;

        await _chunkRepo.DeleteByDocumentAsync(documentId);

        var chunkingModel = await _settingService.GetSettingAsync("Rbl.ChunkingStrategy", "SemanticKernel");
        var chunker = _chunkingFactory.GetStrategy(chunkingModel);
        var chunked = chunker.Chunk(nonEmpty);

        if (chunked.Count == 0) return 0;

        var embeddingModel = await _settingService.GetSettingAsync("Rbl.EmbeddingModel", "Keyword");
        var embedder = _embeddingFactory.GetProvider(embeddingModel);

        var chunks = new List<DocumentChunk>();
        for (int i = 0; i < chunked.Count; i++)
        {
            var c = chunked[i];
            string? vectorJson = null;
            if (embedder != null)
            {
                try
                {
                    var vector = await embedder.GetEmbeddingAsync(c.Text, embeddingModel);
                    vectorJson = JsonSerializer.Serialize(vector);
                }
                catch
                {
                    // Keep chunk without vector; keyword search still works.
                }
            }

            chunks.Add(new DocumentChunk
            {
                DocumentId = documentId,
                SubjectId = subjectId,
                DocumentName = fileName,
                ChunkIndex = i,
                Content = c.Text,
                Page = c.Page,
                VectorJson = vectorJson,
                EmbeddingModel = vectorJson != null ? embeddingModel : null
            });
        }

        await _chunkRepo.InsertManyAsync(chunks);
        return chunked.Count;
    }
}
