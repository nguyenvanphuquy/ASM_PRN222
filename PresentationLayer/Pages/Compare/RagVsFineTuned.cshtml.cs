using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;
using System.Security.Claims;

namespace PresentationLayer.Pages.Compare;

[Authorize(Policy = "LecturerOrAdmin")]
public class RagVsFineTunedModel : PageModel
{
    private readonly IRagVsFineTunedComparisonService _compare;
    private readonly ISubjectService _subjects;

    public RagVsFineTunedModel(IRagVsFineTunedComparisonService compare, ISubjectService subjects)
    {
        _compare = compare;
        _subjects = subjects;
    }

    public List<ServiceLayer.DTOs.SubjectDto> Subjects { get; private set; } = new();
    public RagVsFineTunedResult? Result { get; private set; }

    [BindProperty] public string Question { get; set; } = "";
    [BindProperty] public string? SubjectId { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "RAG vs Fine-tuned";
        ViewData["TopbarTitle"] = "🔬 So sánh RAG vs Fine-tuned";
        Subjects = await _subjects.GetAllAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["Title"] = "RAG vs Fine-tuned";
        ViewData["TopbarTitle"] = "🔬 So sánh RAG vs Fine-tuned";
        Subjects = await _subjects.GetAllAsync();

        if (string.IsNullOrWhiteSpace(Question))
        {
            ModelState.AddModelError(nameof(Question), "Vui lòng nhập câu hỏi để so sánh.");
            return Page();
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        Result = await _compare.CompareAsync(
            Question.Trim(),
            string.IsNullOrEmpty(SubjectId) ? null : SubjectId,
            userId);

        return Page();
    }
}
