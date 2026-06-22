using System.ComponentModel.DataAnnotations;

namespace ServiceLayer.Dtos;

public record LoginResult(bool Success, string? ErrorMessage, string? UserId, string? Username, string? FullName, string? Role, string? AvatarPath, bool CanUploadDocuments = false, string? AssignedSubjectId = null);

public class LoginRequest
{
    [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    public string Password { get; set; } = string.Empty;
}
