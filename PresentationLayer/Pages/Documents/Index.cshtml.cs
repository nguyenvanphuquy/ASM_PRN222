using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Services;

namespace PresentationLayer.Pages.Documents;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IDocumentService _docService;
    private readonly ISubjectService _subjectService;

    public IndexModel(IDocumentService docService, ISubjectService subjectService)
    {
        _docService = docService;
        _subjectService = subjectService;
    }

    public List<ServiceLayer.DTOs.DocumentDto> Documents { get; private set; } = [];
    public List<ServiceLayer.DTOs.SubjectDto> Subjects { get; private set; } = [];

    [BindProperty(SupportsGet = true)] public string? SubjectId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Query { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Tài liệu";
        ViewData["TopbarTitle"] = "📄 Tài liệu";

        Subjects = await _subjectService.GetAllAsync();
        Documents = await _docService.SearchAsync(SubjectId, Query);
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
        var canManage = role == "Admin" || role == "Lecturer" || User.HasClaim("CanUpload", "true");
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


