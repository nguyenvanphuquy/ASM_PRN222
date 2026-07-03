namespace DataAccessLayer.Entities;

/// <summary>
/// Bản ghi giao dịch mua gói (thanh toán giả lập) + cấp quota token cho người dùng.
/// Số dư token khả dụng của một user = Σ(TokensGranted − TokensUsed) trên các bản ghi
/// Status = "Paid" và chưa hết hạn.
/// </summary>
public class PackagePurchase
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    // Chụp lại tên/giá lúc mua để báo cáo không phụ thuộc việc gói bị sửa/xoá về sau.
    public string PackageName { get; set; } = string.Empty;
    public long AmountVnd { get; set; }
    public int TokensGranted { get; set; }
    public int TokensUsed { get; set; }
    // Paid | Pending | Expired | Cancelled
    public string Status { get; set; } = "Paid";
    public string PaymentMethod { get; set; } = "Mock";
    public string TransactionRef { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
}
