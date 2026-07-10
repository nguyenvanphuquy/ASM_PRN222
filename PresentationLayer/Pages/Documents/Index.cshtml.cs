using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Services.Interfaces;

namespace PresentationLayer.Pages.Documents;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IDocumentService _docService;
    private readonly ISubjectService _subjectService;
    private readonly IUserService _userService;

    public IndexModel(IDocumentService docService, ISubjectService subjectService, IUserService userService)
    {
        _docService = docService;
        _subjectService = subjectService;
        _userService = userService;
    }

    public List<ServiceLayer.DTOs.DocumentDto> Documents { get; private set; } = [];
    public List<ServiceLayer.DTOs.SubjectDto> Subjects { get; private set; } = [];
    public Dictionary<string, string> UserNames { get; private set; } = [];

    [BindProperty(SupportsGet = true)] public string? SubjectId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Query { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Tài liệu";
        ViewData["TopbarTitle"] = "📄 Tài liệu";

        Subjects = await _subjectService.GetAllAsync();
        Documents = await _docService.SearchAsync(SubjectId, Query);
        var users = await _userService.GetAllAsync();
        UserNames = users.ToDictionary(u => u.Id, u => string.IsNullOrEmpty(u.FullName) ? u.Username : u.FullName);
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
        // Admin KHÔNG được quản lý tài liệu (kể cả xoá) — chỉ giảng viên.
        if (role == "Admin")
        {
            TempData["Error"] = "Admin không được phép xoá tài liệu.";
            return RedirectToPage();
        }
        var canManage = role == "Lecturer" || User.HasClaim("CanUpload", "true");
        if (!canManage)
        {
            TempData["Error"] = "Bạn không có quyền thực hiện hành động này.";
            return RedirectToPage();
        }

        await _docService.DeleteAsync(id);
        TempData["Success"] = "Đã xoá tài liệu.";
        return RedirectToPage();
    }
}




