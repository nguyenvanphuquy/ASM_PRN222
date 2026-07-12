using System.Text.RegularExpressions;
using DataAccessLayer.Constants;
using DataAccessLayer.Entities;
using DataAccessLayer.Repositories.Interfaces;
using ServiceLayer.Services.Interfaces;

namespace ServiceLayer.Services.Implementations;

public class UserService : IUserService
{
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private readonly IUserRepository _repo;
    private readonly AutoMapper.IMapper _mapper;
    private readonly IAllowedEmailService _allowedEmails;
    private readonly INotificationService _notifier;
    public UserService(IUserRepository repo, IAllowedEmailService allowedEmails, AutoMapper.IMapper mapper, INotificationService notifier)
    {
        _repo = repo;
        _allowedEmails = allowedEmails;
        _mapper = mapper;
        _notifier = notifier;
    }

    public async Task<List<DTOs.UserDto>> GetAllAsync() { var entities = await _repo.GetAllAsync(); return _mapper.Map<List<DTOs.UserDto>>(entities); }
    public async Task<DTOs.UserDto?> GetByIdAsync(string id) { var entity = await _repo.GetByIdAsync(id); return _mapper.Map<DTOs.UserDto>(entity); }

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
        var roleId = await _repo.GetRoleIdByNameAsync(role);
        if (roleId is null)
            return (false, "Role không tồn tại trong hệ thống", null);

        await _repo.CreateAsync(new User
        {
            Username = username.Trim(),
            Email = email,
            FullName = fullName?.Trim() ?? string.Empty,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            RoleId = roleId,
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
        await _notifier.UserChangedAsync("verified", user.Id);
        return (true, null);
    }

    public async Task<(bool, string?)> UpdateRoleAsync(string id, string newRole)
    {
        if (!Roles.All.Contains(newRole)) return (false, "Role không hợp lệ");
        var user = await _repo.GetByIdAsync(id);
        if (user is null) return (false, "User không tồn tại");
        var roleId = await _repo.GetRoleIdByNameAsync(newRole);
        if (roleId is null) return (false, "Role không tồn tại trong hệ thống");
        user.RoleId = roleId;
        // Upload permission only applies to lecturers — clear all subject assignments for other roles.
        if (newRole != Roles.Lecturer)
        {
            user.CanUploadDocuments = false;
            user.AssignedSubjectId = null;
            await _repo.ReplaceAssignedSubjectsAsync(id, Array.Empty<string>());
        }
        await _repo.UpdateAsync(user);
        await _notifier.UserChangedAsync("role", id, newRole);
        return (true, null);
    }

    public Task<List<string>> GetAssignedSubjectIdsAsync(string id) => _repo.GetAssignedSubjectIdsAsync(id);

    public async Task<Dictionary<string, string>> GetSubjectOwnersAsync()
    {
        var all = await _repo.GetAllLecturerSubjectsAsync();
        // Mỗi môn chỉ có một chủ, nên groupBy an toàn.
        return all.GroupBy(x => x.SubjectId).ToDictionary(g => g.Key, g => g.First().UserId);
    }

    /// <summary>
    /// Giao danh sách môn cho một giảng viên. Một giảng viên có thể phụ trách NHIỀU môn,
    /// nhưng mỗi môn chỉ được giao cho ĐÚNG MỘT giảng viên.
    /// </summary>
    public async Task<(bool, string?)> SetAssignedSubjectsAsync(string id, IReadOnlyList<string> subjectIds)
    {
        var user = await _repo.GetByIdAsync(id);
        if (user is null) return (false, "User không tồn tại");
        if (user.Role != Roles.Lecturer) return (false, "Chỉ áp dụng quyền upload cho giảng viên");

        var ids = subjectIds?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList() ?? new List<string>();

        // Kiểm tra độc quyền: môn đã được giao cho GIẢNG VIÊN KHÁC thì không được giao lại.
        var all = await _repo.GetAllLecturerSubjectsAsync();
        var conflict = all.FirstOrDefault(x => x.UserId != id && ids.Contains(x.SubjectId));
        if (conflict != null)
        {
            var holder = await _repo.GetByIdAsync(conflict.UserId);
            return (false, $"Môn học đã được giao cho giảng viên \"{holder?.FullName ?? "khác"}\". Mỗi môn chỉ giao cho duy nhất một giảng viên.");
        }

        await _repo.ReplaceAssignedSubjectsAsync(id, ids);

        // Giữ cột đơn (AssignedSubjectId/CanUploadDocuments) đồng bộ để tương thích hiển thị cũ.
        user.CanUploadDocuments = ids.Count > 0;
        user.AssignedSubjectId = ids.FirstOrDefault();
        await _repo.UpdateAsync(user);

        await _notifier.UserChangedAsync("assign", id, ids.Count.ToString());
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
        await _notifier.UserChangedAsync("deleted", id);
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




