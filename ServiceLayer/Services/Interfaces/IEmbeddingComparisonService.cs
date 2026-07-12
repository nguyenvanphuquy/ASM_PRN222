using ServiceLayer.Dtos;

namespace ServiceLayer.Services.Interfaces;

public interface IEmbeddingComparisonService
{
    /// <summary>
    /// So sánh nhiều embedding model trên cùng câu hỏi + cùng chunk (in-memory, không ghi đè index).
    /// subjectId = null/rỗng → chạy trên tài liệu của TẤT CẢ môn.
    /// </summary>
    Task<EmbeddingComparisonResult> CompareAsync(string question, string? subjectId, string userId);
}
