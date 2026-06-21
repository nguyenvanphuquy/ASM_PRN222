using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Services;

namespace PresentationLayer.Pages.Settings;

[Authorize(Roles = "Admin,Lecturer")]
public class IndexModel : PageModel
{
    private readonly ISystemSettingService _settingService;

    public IndexModel(ISystemSettingService settingService)
    {
        _settingService = settingService;
    }

    [BindProperty]
    public string ChunkingStrategy { get; set; } = "SemanticKernel";
    
    [BindProperty]
    public string EmbeddingModel { get; set; } = "Keyword";

    [BindProperty]
    public string OpenAIApiKey { get; set; } = "";

    [BindProperty]
    public string HuggingFaceApiToken { get; set; } = "";

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Cài đặt RBL";
        ViewData["TopbarTitle"] = "⚙️ Cài đặt Thực nghiệm";
        
        ChunkingStrategy = await _settingService.GetSettingAsync("Rbl.ChunkingStrategy", "SemanticKernel");
        EmbeddingModel = await _settingService.GetSettingAsync("Rbl.EmbeddingModel", "Keyword");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _settingService.SetSettingAsync("Rbl.ChunkingStrategy", ChunkingStrategy, "Chiến lược cắt chunk tài liệu");
        await _settingService.SetSettingAsync("Rbl.EmbeddingModel", EmbeddingModel, "Mô hình nhúng Vector");
        
        // TODO: Save API Keys to appsettings.json or SystemSetting if really needed for the demo
        // For security, usually stored in Secrets or Environment variables, but we can save to DB for ease of RBL testing
        if (!string.IsNullOrEmpty(OpenAIApiKey))
            await _settingService.SetSettingAsync("Rbl.OpenAIApiKey", OpenAIApiKey);
            
        if (!string.IsNullOrEmpty(HuggingFaceApiToken))
            await _settingService.SetSettingAsync("Rbl.HuggingFaceApiToken", HuggingFaceApiToken);

        TempData["Success"] = "✅ Đã lưu cấu hình thực nghiệm thành công.";
        return RedirectToPage();
    }
}
