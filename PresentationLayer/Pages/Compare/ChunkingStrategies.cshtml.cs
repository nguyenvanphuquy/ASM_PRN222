using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;
using System.Security.Claims;

namespace PresentationLayer.Pages.Compare;

[Authorize(Policy = "LecturerOrAdmin")]
public class ChunkingStrategiesModel : PageModel
{
    private readonly IChunkingComparisonService _compare;
    private readonly ISubjectService _subjects;

    public ChunkingStrategiesModel(IChunkingComparisonService compare, ISubjectService subjects)
    {
        _compare = compare;
        _subjects = subjects;
    }

    public List<ServiceLayer.DTOs.SubjectDto> Subjects { get; private set; } = new();
    public ChunkingComparisonResult? Result { get; private set; }

    [BindProperty] public string Question { get; set; } = "";
    [BindProperty] public string? SubjectId { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "So sánh Chunking";
        ViewData["TopbarTitle"] = "✂️ Benchmark Chunking Strategy";
        Subjects = await _subjects.GetAllAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["Title"] = "So sánh Chunking";
        ViewData["TopbarTitle"] = "✂️ Benchmark Chunking Strategy";
        Subjects = await _subjects.GetAllAsync();

        if (string.IsNullOrWhiteSpace(Question))
        {
            ModelState.AddModelError(nameof(Question), "Vui lòng nhập câu hỏi để so sánh.");
            return Page();
        }
        if (string.IsNullOrWhiteSpace(SubjectId))
        {
            ModelState.AddModelError(nameof(SubjectId), "Cần chọn môn học (benchmark chạy trên tài liệu của môn).");
            return Page();
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        Result = await _compare.CompareAsync(Question.Trim(), SubjectId, userId);
        return Page();
    }
}
