using DataAccessLayer.Entities;

namespace ServiceLayer.Services;

public interface IGroqService
{
    Task<string> GenerateAnswerAsync(
        string question,
        IReadOnlyList<DocumentChunk> contextChunks,
        IReadOnlyList<ChatMessage> history,
        CancellationToken ct = default);

    Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default);
}
