using DataAccessLayer.Entities;

namespace ServiceLayer.Services;

public interface IChunkingService
{
    Task<int> ChunkAndSaveAsync(string documentId, string subjectId, string fileName, string extractedText);
}
