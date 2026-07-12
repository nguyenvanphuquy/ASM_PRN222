namespace ServiceLayer.Services.Interfaces;

public interface IChunkingService
{
    /// <summary>Chunk + (optional) embed pages and persist. Returns number of chunks saved.</summary>
    Task<int> ChunkAndSaveAsync(
        string documentId,
        string subjectId,
        string fileName,
        IReadOnlyList<(int Page, string Text)> pages);

    /// <summary>Convenience overload when only joined text is available (treated as page 1).</summary>
    Task<int> ChunkAndSaveAsync(string documentId, string subjectId, string fileName, string extractedText);
}
