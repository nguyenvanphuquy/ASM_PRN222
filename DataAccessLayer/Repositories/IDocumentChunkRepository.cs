using DataAccessLayer.Entities;

namespace DataAccessLayer.Repositories;

public interface IDocumentChunkRepository
{
    Task InsertManyAsync(IEnumerable<DocumentChunk> chunks);
    Task<List<DocumentChunk>> GetByDocumentAsync(string documentId);
    Task<List<(DocumentChunk Chunk, float Score)>> SearchAsync(string query, string? subjectId, int limit, float[]? queryVector = null);
    Task DeleteByDocumentAsync(string documentId);
    Task<long> CountAsync();
}
