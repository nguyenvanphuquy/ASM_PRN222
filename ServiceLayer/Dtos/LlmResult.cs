namespace ServiceLayer.Dtos;

/// <summary>
/// Kết quả một lần gọi LLM kèm số liệu token + độ trễ, phục vụ thống kê & so sánh model.
/// </summary>
public record LlmResult(
    string Content,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    long LatencyMs,
    bool IsError = false);
