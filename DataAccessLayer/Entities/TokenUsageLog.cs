namespace DataAccessLayer.Entities;

/// <summary>
/// Nhật ký tiêu thụ token cho mỗi lần gọi LLM (chat hỏi–đáp, so sánh model, kiểm tra chất lượng…).
/// Là nguồn dữ liệu cho báo cáo "ai đang dùng bao nhiêu token" theo ngày/tuần/tháng.
/// </summary>
public class TokenUsageLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public string Model { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    // Chi phí ước tính (USD) theo đơn giá token của model — dùng để tính lợi nhuận gói.
    public decimal CostUsd { get; set; }
    // chat | compare | quality
    public string Kind { get; set; } = "chat";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
