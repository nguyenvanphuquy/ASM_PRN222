using DataAccessLayer.Entities;

namespace ServiceLayer.Services;

public interface IRetrievalService
{
    Task<List<(DocumentChunk Chunk, float Score)>> SearchAsync(string query, string? subjectId, int limit);
}


