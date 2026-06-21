using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Dtos;
using ServiceLayer.Services;

namespace PresentationLayer.Pages.Documents;

[Authorize]
public class CompareModel : PageModel
{
    private readonly IDocumentService _docService;
    private readonly IFileComparisonService _compareService;

    public CompareModel(IDocumentService docService, IFileComparisonService compareService)
    {
        _docService = docService;
        _compareService = compareService;
    }

    public List<ServiceLayer.DTOs.DocumentDto> AllDocuments { get; private set; } = [];

    // Kết quả so sánh
    public FileComparisonResult? Result { get; private set; }
    public string? CompareError { get; private set; }

    // Mode: so sánh qua DB hay upload 2 file trực tiếp
    [BindProperty] public string Mode { get; set; } = "db"; // "db" | "upload"

    // DB Mode
    [BindProperty] public string? DocId1 { get; set; }
    [BindProperty] public string? DocId2 { get; set; }

    // Upload Mode
    [BindProperty] public IFormFile? File1 { get; set; }
    [BindProperty] public IFormFile? File2 { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "So sánh file bằng AI";
        ViewData["TopbarTitle"] = "🔍 So sánh tài liệu AI";
        AllDocuments = await _docService.GetAllAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["Title"] = "So sánh file bằng AI";
        ViewData["TopbarTitle"] = "🔍 So sánh tài liệu AI";
        AllDocuments = await _docService.GetAllAsync();

        try
        {
            if (Mode == "upload")
            {
                // So sánh 2 file upload trực tiếp
                if (File1 == null || File2 == null || File1.Length == 0 || File2.Length == 0)
                {
                    CompareError = "Vui lòng chọn đủ 2 file để so sánh.";
                    return Page();
                }

                await using var s1 = File1.OpenReadStream();
                await using var s2 = File2.OpenReadStream();

                Result = await _compareService.CompareStreamsAsync(
                    s1, File1.FileName, File1.ContentType,
                    s2, File2.FileName, File2.ContentType);
            }
            else
            {
                // So sánh 2 documents trong DB
                if (string.IsNullOrWhiteSpace(DocId1) || string.IsNullOrWhiteSpace(DocId2))
                {
                    CompareError = "Vui lòng chọn đủ 2 tài liệu để so sánh.";
                    return Page();
                }

                if (DocId1 == DocId2)
                {
                    CompareError = "Vui lòng chọn 2 tài liệu khác nhau.";
                    return Page();
                }

                Result = await _compareService.CompareAsync(DocId1, DocId2);
            }
        }
        catch (Exception ex)
        {
            CompareError = $"Lỗi khi so sánh: {ex.Message}";
        }

        return Page();
    }
}


