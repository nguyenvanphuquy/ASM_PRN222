using ServiceLayer.Dtos;

namespace ServiceLayer.Services.Interfaces;

public interface IChunkingComparisonService
{
    /// <summary>
    /// So sánh 3 chunking strategy trên cùng câu hỏi + cùng bộ tài liệu môn học (in-memory, không ghi đè index).
    /// </summary>
    Task<ChunkingComparisonResult> CompareAsync(string question, string subjectId, string userId);
}
