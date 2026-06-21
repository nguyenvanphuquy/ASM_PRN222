using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Services;

namespace PresentationLayer.Pages.Subjects;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ISubjectService _subjectService;
    public IndexModel(ISubjectService subjectService) => _subjectService = subjectService;
    public List<ServiceLayer.DTOs.SubjectDto> Subjects { get; private set; } = [];

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Môn học";
        ViewData["TopbarTitle"] = "📚 Môn học";
        Subjects = await _subjectService.GetAllAsync();
    }
}


