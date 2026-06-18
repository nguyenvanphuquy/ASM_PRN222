namespace DataAccessLayer.Entities;

/// <summary>
/// Email được Admin cho phép đăng ký (whitelist). Nếu danh sách có ít nhất 1 email, người dùng
/// chỉ đăng ký được khi email của họ nằm trong danh sách này — tương tự AllowedEmail của Assignment 1.
/// Khi danh sách trống, đăng ký mở cho mọi email (giữ tương thích với hành vi cũ).
/// </summary>
public class AllowedEmail
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Email { get; set; } = string.Empty;
    // Ghi chú tuỳ chọn (vd: "Sinh viên K17", "Giảng viên khoa CNTT").
    public string Note { get; set; } = string.Empty;
    // Username của admin đã thêm email này.
    public string AddedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
