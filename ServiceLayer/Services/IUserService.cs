using DataAccessLayer.Entities;

namespace ServiceLayer.Services;

public interface IUserService
{
    Task<List<ServiceLayer.DTOs.UserDto>> GetAllAsync();
    Task<ServiceLayer.DTOs.UserDto?> GetByIdAsync(string id);
    // Tạo tài khoản chưa kích hoạt; trả về token xác thực email để gửi link kích hoạt.
    Task<(bool Success, string? Error, string? VerificationToken)> CreateAsync(string username, string email, string fullName, string password, string role);
    // Kích hoạt tài khoản qua token xác thực email.
    Task<(bool Success, string? Error)> VerifyEmailAsync(string token);
    Task<(bool Success, string? Error)> UpdateRoleAsync(string id, string newRole);
    Task<(bool Success, string? Error)> SetUploadPermissionAsync(string id, bool canUpload, string? subjectId);
    Task<(bool Success, string? Error)> ResetPasswordAsync(string id, string newPassword);
    Task<(bool Success, string? Error)> DeleteAsync(string id);
    Task<(long Total, long Admins, long Lecturers, long Students)> GetCountsAsync();
    Task<(bool Success, string? Error)> UpdateProfileAsync(string id, string fullName, string email, string? bio);
    Task<(bool Success, string? Error)> UpdateAvatarAsync(string id, string avatarPath);
    Task<(bool Success, string? Error)> ChangePasswordAsync(string id, string currentPassword, string newPassword);
}



