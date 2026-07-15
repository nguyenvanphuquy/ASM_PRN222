namespace DataAccessLayer.Entities;

/// <summary>
/// Nhật ký hành động hệ thống (đăng nhập, upload/xoá tài liệu, mua gói…).
/// Persist để trang AuditLogs vẫn xem được sau khi entity gốc đã bị xoá.
/// </summary>
public class SystemActivity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Icon { get; set; } = "•";
    public string Category { get; set; } = "";
    public string Actor { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
