using ServiceLayer.Dtos;

namespace ServiceLayer.Services.Interfaces;

/// <summary>
/// So sánh nhiều model LLM (≥ 3) trên cùng một câu hỏi + cùng ngữ cảnh RAG,
/// đo token / độ trễ / chi phí — phục vụ mục "Benchmarks/Metrics" của đề bài.
/// </summary>
public interface IModelComparisonService
{
    Task<ModelComparisonResult> CompareAsync(string question, string? subjectId, string userId);
}
