using DataAccessLayer.Entities;

namespace DataAccessLayer.Repositories;

public interface IDocumentChunkRepository
{
    Task InsertManyAsync(IEnumerable<DocumentChunk> chunks);
    Task<List<DocumentChunk>> GetByDocumentAsync(string documentId);
    Task<List<DocumentChunk>> SearchAsync(string query, string? subjectId, int limit);
    Task DeleteByDocumentAsync(string documentId);
    Task<long> CountAsync();
}
