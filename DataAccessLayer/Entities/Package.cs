namespace DataAccessLayer.Entities;

/// <summary>
/// Gói token bán cho người dùng. Người dùng mua gói để có quota token dùng cho hỏi–đáp AI.
/// </summary>
public class Package
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    // Giá bán (VND). 0 = gói miễn phí.
    public long PriceVnd { get; set; }
    // Số token được cấp khi mua gói.
    public int TokenQuota { get; set; }
    // Số ngày hiệu lực kể từ lúc mua (0 = không giới hạn thời gian).
    public int DurationDays { get; set; }
    public bool IsActive { get; set; } = true;
    // Gói nổi bật (đánh dấu "Phổ biến" trên cửa hàng).
    public bool IsPopular { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // Xoá mềm: gói bị "xoá" vẫn giữ lại row để lịch sử mua (PackagePurchases) không mồ côi
    // (khoá ngoại PackagePurchases → Packages). Bị ẩn khỏi cửa hàng & trang quản trị qua query filter.
    public bool IsDeleted { get; set; }
}
