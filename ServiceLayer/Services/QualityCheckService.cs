using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ServiceLayer.Services;

public class QualityCheckService : IQualityCheckService
{
    private readonly IGroqService _llm;
    private readonly ILogger<QualityCheckService> _logger;

    public QualityCheckService(IGroqService llm, ILogger<QualityCheckService> logger)
    {
        _llm = llm;
        _logger = logger;
    }

    public async Task<QualityCheckResult> CheckQualityAsync(string extractedText)
    {
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            return new QualityCheckResult
            {
                Score = 0,
                Summary = "Tài liệu rỗng hoặc không trích xuất được nội dung.",
                Warnings = "Văn bản hoàn toàn trống."
            };
        }

        if (extractedText.Length < 50)
        {
            return new QualityCheckResult
            {
                Score = 10,
                Summary = "Tài liệu quá ngắn.",
                Warnings = "Nội dung trích xuất quá ngắn, có thể do file ảnh hoặc lỗi định dạng."
            };
        }

        var prompt = $@"
Bạn là một AI phân tích chất lượng tài liệu học thuật. Dưới đây là nội dung văn bản trích xuất từ tài liệu:

---
{extractedText.Substring(0, Math.Min(extractedText.Length, 4000))} // Chỉ lấy 4000 ký tự đầu để đánh giá
---

Vui lòng đánh giá chất lượng tài liệu này và trả về JSON theo đúng định dạng sau, KHÔNG thêm bất kỳ từ ngữ nào khác:
{{
    ""score"": 85, // Từ 0 đến 100
    ""summary"": ""Tóm tắt nội dung chính (1-2 câu)"",
    ""warnings"": ""Các cảnh báo nếu có (ví dụ: lỗi OCR nhiều, văn bản lộn xộn, thiếu dấu câu), nếu không có thì để trống""
}}";

        try
        {
            var responseText = await _llm.GenerateTextAsync(prompt);
            
            // Tìm block JSON trong trường hợp LLM trả thêm text
            var startIndex = responseText.IndexOf("{");
            var endIndex = responseText.LastIndexOf("}");
            
            if (startIndex >= 0 && endIndex > startIndex)
            {
                var json = responseText.Substring(startIndex, endIndex - startIndex + 1);
                var result = JsonSerializer.Deserialize<JsonElement>(json);
                
                return new QualityCheckResult
                {
                    Score = result.GetProperty("score").GetInt32(),
                    Summary = result.GetProperty("summary").GetString() ?? "Không có tóm tắt.",
                    Warnings = result.GetProperty("warnings").GetString() ?? ""
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi AI để đánh giá chất lượng tài liệu");
        }

        // Fallback
        return new QualityCheckResult
        {
            Score = 50,
            Summary = "Đã trích xuất nội dung nhưng không thể AI chấm điểm tự động.",
            Warnings = "Lỗi kết nối AI hoặc AI trả về sai định dạng."
        };
    }
}
