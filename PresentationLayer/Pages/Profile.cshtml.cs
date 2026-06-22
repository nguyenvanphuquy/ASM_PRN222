using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Services.Interfaces;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace PresentationLayer.Pages;

[Authorize]
public class ProfileModel : PageModel
{
    private readonly IUserService _userService;
    public ProfileModel(IUserService userService) => _userService = userService;

    public ServiceLayer.DTOs.UserDto? UserInfo { get; private set; }

    [BindProperty] public string FullName { get; set; } = "";
    [BindProperty] public string Email { get; set; } = "";
    [BindProperty] public string? Bio { get; set; }

    [BindProperty] public string? CurrentPassword { get; set; }
    [BindProperty] public string? NewPassword { get; set; }

    [BindProperty] public IFormFile? AvatarFile { get; set; }

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
        
        if (ok)
        {
            // Update FullName claim in cookie if changed
            var identity = (ClaimsIdentity)User.Identity!;
            var nameClaim = identity.FindFirst("FullName");
            if (nameClaim != null) identity.RemoveClaim(nameClaim);
            identity.AddClaim(new Claim("FullName", FullName));
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            TempData["Success"] = "Đã cập nhật hồ sơ.";
        }
        else
        {
            TempData["Error"] = err;
        }
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

    public async Task<IActionResult> OnPostUploadAvatarAsync()
    {
        if (AvatarFile == null || AvatarFile.Length == 0)
        {
            TempData["Error"] = "Vui lòng chọn một file ảnh.";
            return RedirectToPage();
        }

        if (!AvatarFile.ContentType.StartsWith("image/"))
        {
            TempData["Error"] = "Chỉ chấp nhận file hình ảnh.";
            return RedirectToPage();
        }

        var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        var webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        if (!Directory.Exists(webRoot))
        {
            webRoot = Path.Combine(Directory.GetCurrentDirectory(), "PresentationLayer", "wwwroot");
        }

        var dir = Path.Combine(webRoot, "uploads", "avatars");
        Directory.CreateDirectory(dir);

        var ext = Path.GetExtension(AvatarFile.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".png";
        var fileName = $"{Guid.NewGuid():N}{ext}";

        var filePath = Path.Combine(dir, fileName);
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await AvatarFile.CopyToAsync(stream);
        }

        var avatarPath = $"/uploads/avatars/{fileName}";
        var (ok, err) = await _userService.UpdateAvatarAsync(id, avatarPath);

        if (ok)
        {
            // Update cookie claim
            var identity = (ClaimsIdentity)User.Identity!;
            var avatarClaim = identity.FindFirst("AvatarPath");
            if (avatarClaim != null) identity.RemoveClaim(avatarClaim);
            identity.AddClaim(new Claim("AvatarPath", avatarPath));
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

            TempData["Success"] = "Đã cập nhật ảnh đại diện thành công.";
        }
        else
        {
            TempData["Error"] = err ?? "Không thể cập nhật ảnh đại diện.";
        }

        return RedirectToPage();
    }
}


