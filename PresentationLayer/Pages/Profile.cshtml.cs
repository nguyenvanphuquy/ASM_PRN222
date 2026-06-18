using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Services;
using System.Security.Claims;

namespace PresentationLayer.Pages;

[Authorize]
public class ProfileModel : PageModel
{
    private readonly IUserService _userService;
    public ProfileModel(IUserService userService) => _userService = userService;

    public User? UserInfo { get; private set; }

    [BindProperty] public string FullName { get; set; } = "";
    [BindProperty] public string Email { get; set; } = "";
    [BindProperty] public string? Bio { get; set; }

    [BindProperty] public string? CurrentPassword { get; set; }
    [BindProperty] public string? NewPassword { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Hồ sơ";
        ViewData["TopbarTitle"] = "👤 Hồ sơ cá nhân";
        var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        UserInfo = await _userService.GetByIdAsync(id);
        if (UserInfo != null) { FullName = UserInfo.FullName; Email = UserInfo.Email; Bio = UserInfo.Bio; }
    }

    public async Task<IActionResult> OnPostUpdateAsync()
    {
        var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        var (ok, err) = await _userService.UpdateProfileAsync(id, FullName, Email, Bio);
        TempData[ok ? "Success" : "Error"] = ok ? "Đã cập nhật hồ sơ." : err;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPasswordAsync()
    {
        var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        if (string.IsNullOrWhiteSpace(CurrentPassword) || string.IsNullOrWhiteSpace(NewPassword))
        {
            TempData["Error"] = "Vui lòng nhập đầy đủ mật khẩu.";
            return RedirectToPage();
        }
        var (ok, err) = await _userService.ChangePasswordAsync(id, CurrentPassword, NewPassword);
        TempData[ok ? "Success" : "Error"] = ok ? "Đã đổi mật khẩu." : err;
        return RedirectToPage();
    }
}
