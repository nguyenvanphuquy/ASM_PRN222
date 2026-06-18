namespace DataAccessLayer.Entities;

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "Student";
    // Lecturers may only upload documents once an admin grants this. Admins can always upload.
    public bool CanUploadDocuments { get; set; }
    // The single subject a granted lecturer is allowed to upload documents to.
    public string? AssignedSubjectId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? AvatarPath { get; set; }
    public string? Bio { get; set; }
    // Tài khoản do Admin tạo phải xác thực email mới được kích hoạt & đăng nhập.
    // Tài khoản seed mặc định coi như đã xác thực.
    public bool IsEmailVerified { get; set; }
    // Token một lần dùng để xác thực email (null sau khi đã xác thực).
    public string? EmailVerificationToken { get; set; }
}
