using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Services.Interfaces;

namespace PresentationLayer.Pages.AllowedEmails;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly IAllowedEmailService _allowedEmailService;

    public IndexModel(IAllowedEmailService allowedEmailService)
    {
        _allowedEmailService = allowedEmailService;
    }

    public List<AllowedEmail> AllowedEmails { get; private set; } = [];
    public bool IsWhitelistEnabled { get; private set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Whitelist Email";
        ViewData["TopbarTitle"] = "📧 Whitelist Email đăng ký";

        AllowedEmails = await _allowedEmailService.GetAllAsync();
        IsWhitelistEnabled = await _allowedEmailService.IsEnabledAsync();
    }

    public async Task<IActionResult> OnPostAddAsync(string email, string? note)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["Error"] = "Vui lòng nhập email.";
            return RedirectToPage();
        }

        var addedBy = User.Identity?.Name ?? "admin";
        var (ok, err) = await _allowedEmailService.AddAsync(email.Trim(), note, addedBy);
        if (!ok)
        {
            TempData["Error"] = err;
        }
        else
        {
            TempData["Success"] = "Đã thêm email vào danh sách cho phép đăng ký.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        await _allowedEmailService.DeleteAsync(id);
        TempData["Success"] = "Đã xoá email khỏi whitelist.";
        return RedirectToPage();
    }
}
