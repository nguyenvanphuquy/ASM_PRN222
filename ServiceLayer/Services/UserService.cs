using System.Text.RegularExpressions;
using DataAccessLayer.Constants;
using DataAccessLayer.Entities;
using DataAccessLayer.Repositories;

namespace ServiceLayer.Services;

public class UserService : IUserService
{
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private readonly IUserRepository _repo;
    private readonly IAllowedEmailService _allowedEmails;
    public UserService(IUserRepository repo, IAllowedEmailService allowedEmails)
    {
        _repo = repo;
        _allowedEmails = allowedEmails;
    }

    public Task<List<User>> GetAllAsync() => _repo.GetAllAsync();
    public Task<User?> GetByIdAsync(string id) => _repo.GetByIdAsync(id);

    public async Task<(bool, string?, string?)> CreateAsync(string username, string email, string fullName, string password, string role)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return (false, "Username và mật khẩu bắt buộc", null);
        if (password.Length < 6)
            return (false, "Mật khẩu tối thiểu 6 ký tự", null);
        // Email bắt buộc & hợp lệ — hệ thống cần gửi thông tin đăng nhập + link kích hoạt cho người dùng.
        email = email?.Trim() ?? string.Empty;
        if (!EmailRegex.IsMatch(email))
            return (false, "Email không hợp lệ (bắt buộc để gửi thông tin tài khoản)", null);
        // Admin chỉ được tạo tài khoản cho email nằm trong whitelist (whitelist trống = cho phép mọi email).
        if (!await _allowedEmails.IsAllowedAsync(email))
            return (false, "Email này không nằm trong whitelist. Hãy thêm email vào danh sách cho phép trước khi tạo tài khoản.", null);
        if (!Roles.All.Contains(role))
            return (false, "Role không hợp lệ", null);
        if (await _repo.GetByUsernameAsync(username.Trim()) is not null)
            return (false, "Username đã tồn tại", null);

        // Tài khoản tạo ra ở trạng thái chưa kích hoạt; người dùng phải xác thực email mới đăng nhập được.
        var token = Guid.NewGuid().ToString("N");
        await _repo.CreateAsync(new User
        {
            Username = username.Trim(),
            Email = email,
            FullName = fullName?.Trim() ?? string.Empty,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role,
            IsEmailVerified = false,
            EmailVerificationToken = token
        });
        return (true, null, token);
    }

    public async Task<(bool, string?)> VerifyEmailAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return (false, "Liên kết kích hoạt không hợp lệ.");
        var user = await _repo.GetByVerificationTokenAsync(token.Trim());
        if (user is null)
            return (false, "Liên kết kích hoạt không hợp lệ hoặc đã được sử dụng.");
        if (user.IsEmailVerified)
            return (true, null);
        user.IsEmailVerified = true;
        user.EmailVerificationToken = null;
        await _repo.UpdateAsync(user);
        return (true, null);
    }

    public async Task<(bool, string?)> UpdateRoleAsync(string id, string newRole)
    {
        if (!Roles.All.Contains(newRole)) return (false, "Role không hợp lệ");
        var user = await _repo.GetByIdAsync(id);
        if (user is null) return (false, "User không tồn tại");
        user.Role = newRole;
        // Upload permission only applies to lecturers — clear it for other roles.
        if (newRole != Roles.Lecturer)
        {
            user.CanUploadDocuments = false;
            user.AssignedSubjectId = null;
        }
        await _repo.UpdateAsync(user);
        return (true, null);
    }

    public async Task<(bool, string?)> SetUploadPermissionAsync(string id, bool canUpload, string? subjectId)
    {
        var user = await _repo.GetByIdAsync(id);
        if (user is null) return (false, "User không tồn tại");
        if (user.Role != Roles.Lecturer) return (false, "Chỉ áp dụng quyền upload cho giảng viên");

        if (canUpload)
        {
            // Granting requires choosing exactly one subject the lecturer may upload to.
            if (string.IsNullOrWhiteSpace(subjectId))
                return (false, "Vui lòng chọn bộ môn (môn học) khi cấp quyền upload");
            user.CanUploadDocuments = true;
            user.AssignedSubjectId = subjectId;
        }
        else
        {
            user.CanUploadDocuments = false;
            user.AssignedSubjectId = null;
        }
        await _repo.UpdateAsync(user);
        return (true, null);
    }

    public async Task<(bool, string?)> ResetPasswordAsync(string id, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            return (false, "Mật khẩu tối thiểu 6 ký tự");
        var user = await _repo.GetByIdAsync(id);
        if (user is null) return (false, "User không tồn tại");
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _repo.UpdateAsync(user);
        return (true, null);
    }

    public async Task<(bool, string?)> DeleteAsync(string id)
    {
        var user = await _repo.GetByIdAsync(id);
        if (user is null) return (false, "User không tồn tại");
        if (user.Role == Roles.Admin && await _repo.CountByRoleAsync(Roles.Admin) <= 1)
            return (false, "Không thể xoá Admin cuối cùng");
        await _repo.DeleteAsync(id);
        return (true, null);
    }

    public async Task<(bool, string?)> UpdateProfileAsync(string id, string fullName, string email, string? bio)
    {
        var user = await _repo.GetByIdAsync(id);
        if (user is null) return (false, "User không tồn tại");
        user.FullName = fullName.Trim();
        user.Email = email?.Trim() ?? string.Empty;
        user.Bio = bio?.Trim();
        await _repo.UpdateAsync(user);
        return (true, null);
    }

    public async Task<(bool, string?)> UpdateAvatarAsync(string id, string avatarPath)
    {
        var user = await _repo.GetByIdAsync(id);
        if (user is null) return (false, "User không tồn tại");
        user.AvatarPath = avatarPath;
        await _repo.UpdateAsync(user);
        return (true, null);
    }

    public async Task<(bool, string?)> ChangePasswordAsync(string id, string currentPassword, string newPassword)
    {
        var user = await _repo.GetByIdAsync(id);
        if (user is null) return (false, "User không tồn tại");
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            return (false, "Mật khẩu hiện tại không đúng");
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            return (false, "Mật khẩu mới phải ít nhất 6 ký tự");
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _repo.UpdateAsync(user);
        return (true, null);
    }

    public async Task<(long, long, long, long)> GetCountsAsync()
    {
        var total = await _repo.CountAsync();
        var admins = await _repo.CountByRoleAsync(Roles.Admin);
        var lecturers = await _repo.CountByRoleAsync(Roles.Lecturer);
        var students = await _repo.CountByRoleAsync(Roles.Student);
        return (total, admins, lecturers, students);
    }
}
