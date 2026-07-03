namespace ServiceLayer.Settings;

/// <summary>Thông tin một model LLM: id gọi API, tên hiển thị, đơn giá token (USD / 1 triệu token).</summary>
public record ModelInfo(string Id, string DisplayName, string Vendor, double InputUsdPer1M, double OutputUsdPer1M);

/// <summary>
/// Danh mục các model dùng để so sánh (benchmark). Tối thiểu 3 model theo yêu cầu đề bài.
/// Đơn giá tham khảo theo bảng giá công khai của Groq.
/// </summary>
public static class ModelCatalog
{
    public static readonly IReadOnlyList<ModelInfo> All = new[]
    {
        new ModelInfo("llama-3.3-70b-versatile", "Llama 3.3 70B Versatile", "Meta · Groq",   0.59, 0.79),
        new ModelInfo("llama-3.1-8b-instant",   "Llama 3.1 8B Instant",    "Meta · Groq",   0.05, 0.08),
        new ModelInfo("openai/gpt-oss-20b",     "GPT-OSS 20B",             "OpenAI · Groq", 0.10, 0.50),
    };

    public static ModelInfo Get(string id)
        => All.FirstOrDefault(m => m.Id == id) ?? new ModelInfo(id, id, "Groq", 0.50, 0.50);

    /// <summary>Ước tính chi phí (USD) cho một lần gọi dựa trên số token vào/ra.</summary>
    public static decimal EstimateCostUsd(string model, int promptTokens, int completionTokens)
    {
        var info = Get(model);
        var cost = promptTokens / 1_000_000.0 * info.InputUsdPer1M
                 + completionTokens / 1_000_000.0 * info.OutputUsdPer1M;
        return (decimal)cost;
    }
}
