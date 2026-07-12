using ServiceLayer.Dtos;

namespace ServiceLayer.Services.Interfaces;

public interface IEmbeddingComparisonService
{
    /// <summary>
    /// So sánh nhiều embedding model trên cùng câu hỏi + cùng chunk (in-memory, không ghi đè index).
    /// </summary>
    Task<EmbeddingComparisonResult> CompareAsync(string question, string subjectId, string userId);
}
