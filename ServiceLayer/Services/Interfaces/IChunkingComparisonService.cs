using ServiceLayer.Dtos;

namespace ServiceLayer.Services.Interfaces;

public interface IChunkingComparisonService
{
    /// <summary>
    /// So sánh 3 chunking strategy trên cùng câu hỏi + cùng bộ tài liệu (in-memory, không ghi đè index).
    /// subjectId = null/rỗng → chạy trên tài liệu của TẤT CẢ môn.
    /// </summary>
    Task<ChunkingComparisonResult> CompareAsync(string question, string? subjectId, string userId);
}
