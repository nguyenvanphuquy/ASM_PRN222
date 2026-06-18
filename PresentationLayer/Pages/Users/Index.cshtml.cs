using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Services;

namespace PresentationLayer.Pages.Users;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly IUserService _userService;
    private readonly ISubjectService _subjectService;

    public IndexModel(IUserService userService, ISubjectService subjectService)
    {
        _userService = userService;
        _subjectService = subjectService;
    }

    public List<User> Users { get; private set; } = [];
    public List<Subject> Subjects { get; private set; } = [];

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Người dùng";
        ViewData["TopbarTitle"] = "👥 Quản lý người dùng";
        Users = await _userService.GetAllAsync();
        Subjects = await _subjectService.GetAllAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        var (ok, err) = await _userService.DeleteAsync(id);
        TempData[ok ? "Success" : "Error"] = ok ? "Đã xoá người dùng." : err;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAssignSubjectAsync(string id, string subjectId)
    {
        var (ok, err) = await _userService.SetUploadPermissionAsync(id, true, subjectId);
        TempData[ok ? "Success" : "Error"] = ok ? "Đã phân công môn học thành công." : err;
        return RedirectToPage();
    }
}
