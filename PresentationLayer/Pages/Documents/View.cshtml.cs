using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Services;
using DataAccessLayer.Entities;

namespace PresentationLayer.Pages.Documents;

public class ViewModel : PageModel
{
    private readonly IDocumentService _documentService;

    public ViewModel(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public ServiceLayer.DTOs.DocumentDto Document { get; set; } = default!;
    public List<ServiceLayer.DTOs.DocumentChunkDto> Chunks { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var doc = await _documentService.GetByIdAsync(id);
        if (doc == null)
            return NotFound();

        Document = doc;
        Chunks = await _documentService.GetChunksAsync(id);

        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        await _documentService.ApproveAsync(id);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRejectAsync(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        await _documentService.RejectAsync(id);
        return RedirectToPage(new { id });
    }
}



