using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Services.Interfaces;

namespace PresentationLayer.Pages.Users;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly IUserService _userService;
    private readonly ISubjectService _subjectService;
    private readonly IEmailService _emailService;

    public IndexModel(IUserService userService, ISubjectService subjectService, IEmailService emailService)
    {
        _userService = userService;
        _subjectService = subjectService;
        _emailService = emailService;
    }

    public List<ServiceLayer.DTOs.UserDto> Users { get; private set; } = [];
    public List<ServiceLayer.DTOs.SubjectDto> Subjects { get; private set; } = [];
    // subjectId -> userId của giảng viên đang phụ trách môn đó (để tick/khoá checkbox).
    public Dictionary<string, string> SubjectOwners { get; private set; } = new();

    // Thống kê nhanh theo vai trò (hiển thị stat cards ở đầu trang)
    public int TotalCount { get; private set; }
    public int AdminCount { get; private set; }
    public int LecturerCount { get; private set; }
    public int StudentCount { get; private set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Người dùng";
        ViewData["TopbarTitle"] = "👥 Quản lý người dùng";
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        Users = await _userService.GetAllAsync();
        Subjects = await _subjectService.GetAllAsync();
        SubjectOwners = await _userService.GetSubjectOwnersAsync();

        TotalCount = Users.Count;
        AdminCount = Users.Count(u => u.Role == "Admin");
        LecturerCount = Users.Count(u => u.Role == "Lecturer");
        StudentCount = Users.Count(u => u.Role == "Student");
    }

    public async Task<IActionResult> OnPostCreateAsync(string username, string email, string fullName, string password, string role)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(password))
        {
            TempData["Error"] = "Vui lòng điền đầy đủ thông tin tạo tài khoản.";
            return RedirectToPage();
        }

        var (ok, err, token) = await _userService.CreateAsync(
            username.Trim(), email.Trim(), fullName.Trim(), password, string.IsNullOrWhiteSpace(role) ? "Student" : role);

        if (!ok)
        {
            TempData["Error"] = err;
            return RedirectToPage();
        }

        // Tài khoản tạo ra đang ở trạng thái CHƯA kích hoạt → phải gửi email kèm
        // thông tin đăng nhập + link kích hoạt thì người dùng mới đăng nhập được.
        var verifyUrl = Url.Page("/Auth/VerifyEmail", pageHandler: null,
            values: new { token }, protocol: Request.Scheme) ?? $"/Auth/VerifyEmail?token={token}";

        try
        {
            await _emailService.SendAccountCreatedAsync(email.Trim(), fullName.Trim(), username.Trim(), password, verifyUrl);
            TempData["Success"] = $"Đã tạo tài khoản {username} và gửi email kích hoạt + thông tin đăng nhập tới {email.Trim()}.";
        }
        catch (Exception ex)
        {
            // Không gửi được email (chưa cấu hình SMTP / lỗi mạng) → hiển thị link để Admin gửi thủ công.
            TempData["Error"] = $"Đã tạo tài khoản {username} nhưng KHÔNG gửi được email ({ex.Message}). " +
                                $"Hãy gửi thủ công link kích hoạt cho người dùng: {verifyUrl}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostChangeRoleAsync(string id, string role)
    {
        var (ok, err) = await _userService.UpdateRoleAsync(id, role);
        TempData[ok ? "Success" : "Error"] = ok ? "Đã cập nhật vai trò." : err;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditUserAsync(string id, string fullName, string email, string? newPassword)
    {
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
        {
            TempData["Error"] = "Họ tên và email là bắt buộc.";
            return RedirectToPage();
        }

        var (ok, err) = await _userService.UpdateProfileAsync(id, fullName, email, null);
        if (!ok)
        {
            TempData["Error"] = err;
            return RedirectToPage();
        }

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            if (newPassword.Length < 6)
            {
                TempData["Warning"] = "Hồ sơ đã cập nhật nhưng mật khẩu không đổi (tối thiểu 6 ký tự).";
                return RedirectToPage();
            }

            var user = await _userService.GetByIdAsync(id);
            var (pwOk, pwErr) = await _userService.ResetPasswordAsync(id, newPassword);
            
            if (pwOk && user != null)
            {
                try 
                {
                    await _emailService.SendPasswordResetByAdminAsync(email, fullName, user.Username, newPassword);
                    TempData["Success"] = $"Đã cập nhật hồ sơ & đổi mật khẩu. Đã gửi email tới {email}.";
                }
                catch (Exception ex)
                {
                    TempData["Warning"] = $"Đã cập nhật hồ sơ & đổi mật khẩu nhưng KHÔNG gửi được email ({ex.Message}).";
                }
            }
            else
            {
                TempData["Warning"] = $"Hồ sơ đã cập nhật nhưng lỗi đổi mật khẩu: {pwErr}";
            }
        }
        else
        {
            TempData["Success"] = "Đã cập nhật hồ sơ thành công.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        var (ok, err) = await _userService.DeleteAsync(id);
        TempData[ok ? "Success" : "Error"] = ok ? "Đã xoá người dùng." : err;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAssignSubjectsAsync(string id, List<string> subjectIds)
    {
        var (ok, err) = await _userService.SetAssignedSubjectsAsync(id, subjectIds ?? new List<string>());
        TempData[ok ? "Success" : "Error"] = ok
            ? (subjectIds is { Count: > 0 } ? $"Đã giao {subjectIds.Count} môn cho giảng viên." : "Đã thu hồi toàn bộ môn của giảng viên.")
            : err;
        return RedirectToPage();
    }
}


