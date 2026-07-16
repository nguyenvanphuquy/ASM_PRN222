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
        new ModelInfo("gpt-oss-120b", "GPT-OSS 120B", "OpenAI · Cerebras", 0.35, 0.75),
        new ModelInfo("gemma-4-31b",  "Gemma 4 31B",  "Google · Cerebras", 0.99, 1.49),
        new ModelInfo("zai-glm-4.7",  "GLM 4.7",      "Z.ai · Cerebras",   2.25, 2.75),
    };

    /// <summary>Tỷ giá quy đổi USD→VND để hiển thị chi phí token (khớp với tính doanh thu/lợi nhuận gói).</summary>
    public const decimal UsdToVnd = 25_000m;

    /// <summary>Quy đổi chi phí USD sang VND, làm tròn tới đồng.</summary>
    public static long ToVnd(decimal usd) => (long)Math.Round(usd * UsdToVnd);

    public static ModelInfo Get(string id)
        => All.FirstOrDefault(m => m.Id == id) ?? new ModelInfo(id, id, "Cerebras", 0.50, 0.50);

    /// <summary>Ước tính chi phí (USD) cho một lần gọi dựa trên số token vào/ra.</summary>
    public static decimal EstimateCostUsd(string model, int promptTokens, int completionTokens)
    {
        var info = Get(model);
        var cost = promptTokens / 1_000_000.0 * info.InputUsdPer1M
                 + completionTokens / 1_000_000.0 * info.OutputUsdPer1M;
        return (decimal)cost;
    }
}
