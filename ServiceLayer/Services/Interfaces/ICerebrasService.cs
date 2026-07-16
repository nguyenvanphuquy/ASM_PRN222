using DataAccessLayer.Entities;
using ServiceLayer.Dtos;

namespace ServiceLayer.Services.Interfaces;

public interface ICerebrasService
{
    /// <summary>Sinh câu trả lời RAG bằng model mặc định, kèm số token đã dùng.</summary>
    Task<LlmResult> GenerateAnswerAsync(
        string question,
        IReadOnlyList<DocumentChunk> contextChunks,
        IReadOnlyList<ChatMessage> history,
        CancellationToken ct = default);

    /// <summary>Sinh câu trả lời RAG bằng một model cụ thể (dùng cho tính năng so sánh model).</summary>
    Task<LlmResult> GenerateAnswerWithModelAsync(
        string model,
        string question,
        IReadOnlyList<DocumentChunk> contextChunks,
        IReadOnlyList<ChatMessage> history,
        CancellationToken ct = default);

    /// <summary>Sinh văn bản tự do (dùng cho kiểm tra chất lượng tài liệu).</summary>
    Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default);

    /// <summary>
    /// Trả lời kiểu parametric / fine-tuned (không đưa chunk tài liệu vào prompt).
    /// Dùng cho so sánh RBL: RAG vs Fine-tuned.
    /// </summary>
    Task<LlmResult> GenerateParametricAnswerAsync(
        string question,
        string? subjectName = null,
        CancellationToken ct = default);
}
