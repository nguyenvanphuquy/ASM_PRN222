using System.ComponentModel.DataAnnotations;

namespace ServiceLayer.Dtos;

public class UserCreateRequest
{
    [Required(ErrorMessage = "Username không được để trống")]
    [StringLength(100, ErrorMessage = "Username tối đa 100 ký tự")]
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

    [Required(ErrorMessage = "Role không được để trống")]
    public string Role { get; set; } = "Student";
}

public class UserUpdateRequest
{
    [Required(ErrorMessage = "Họ tên không được để trống")]
    [StringLength(200, ErrorMessage = "Họ tên tối đa 200 ký tự")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    [StringLength(200, ErrorMessage = "Email tối đa 200 ký tự")]
    public string Email { get; set; } = string.Empty;

    public string? Bio { get; set; }
}

public class UserRoleUpdateRequest
{
    [Required(ErrorMessage = "Role không được để trống")]
    public string Role { get; set; } = string.Empty;
}

public class UserUploadPermissionRequest
{
    public bool CanUpload { get; set; }
    public string? SubjectId { get; set; }
}

public class UserResetPasswordRequest
{
    [Required(ErrorMessage = "Mật khẩu mới không được để trống")]
    [MinLength(6, ErrorMessage = "Mật khẩu mới tối thiểu 6 ký tự")]
    public string NewPassword { get; set; } = string.Empty;
}

public class UserResponse
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool CanUploadDocuments { get; set; }
    public string? AssignedSubjectId { get; set; }
    public string? AvatarPath { get; set; }
    public string? Bio { get; set; }
    public bool IsEmailVerified { get; set; }
    public DateTime CreatedAt { get; set; }
}
