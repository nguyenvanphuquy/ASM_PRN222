using ServiceLayer.Dtos;

namespace ServiceLayer.Services.Interfaces;

/// <summary>
/// So sánh RAG (retrieve + generate có trích dẫn) với Fine-tuned / parametric
/// (trả lời từ kiến thức nội tại, không retrieval) — phục vụ module RBL.
/// </summary>
public interface IRagVsFineTunedComparisonService
{
    Task<RagVsFineTunedResult> CompareAsync(string question, string? subjectId, string userId);
}
