using System.Text.Json;
using DataAccessLayer.Entities;
using DataAccessLayer.Repositories;
using ServiceLayer.Services.Embeddings;

namespace ServiceLayer.Services;

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

    public async Task<int> ChunkAndSaveAsync(string documentId, string subjectId, string fileName, string extractedText)
    {
        if (string.IsNullOrWhiteSpace(extractedText)) return 0;

        // Xóa chunk cũ (nếu có)
        await _chunkRepo.DeleteByDocumentAsync(documentId);

        // Chúng ta giả định trang là 1 cho toàn bộ extracted text nếu nó không được chia trang.
        // Tuy nhiên TextExtractor có trả về danh sách trang. Để tái sử dụng ChunkingFactory,
        // chúng ta có thể truyền vào 1 list gồm 1 trang.
        var pages = new List<(int Page, string Text)> { (1, extractedText) };

        var chunkingModel = await _settingService.GetSettingAsync("Rbl.ChunkingStrategy", "SemanticKernel");
        var chunker = _chunkingFactory.GetStrategy(chunkingModel);
        var chunked = chunker.Chunk(pages);

        if (chunked.Count > 0)
        {
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
                    catch { /* Fallback to null if API fails */ }
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
                    EmbeddingModel = embedder != null ? embeddingModel : null
                });
            }
            await _chunkRepo.InsertManyAsync(chunks);
        }

        return chunked.Count;
    }
}


