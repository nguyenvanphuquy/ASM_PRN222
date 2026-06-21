using System.ComponentModel.DataAnnotations;

namespace ServiceLayer.Dtos;

public record LoginResult(bool Success, string? ErrorMessage, string? UserId, string? Username, string? FullName, string? Role, string? AvatarPath, bool CanUploadDocuments = false, string? AssignedSubjectId = null);
public record RegisterResult(bool Success, string? ErrorMessage);

public class LoginRequest
{
    [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
    [StringLength(100, ErrorMessage = "Tên đăng nhập tối đa 100 ký tự")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    [StringLength(200, ErrorMessage = "Email tối đa 200 ký tự")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Họ tên không được để trống")]
    [StringLength(200, ErrorMessage = "Họ tên tối đa 200 ký tự")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    [MinLength(6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
    public string Password { get; set; } = string.Empty;
}
