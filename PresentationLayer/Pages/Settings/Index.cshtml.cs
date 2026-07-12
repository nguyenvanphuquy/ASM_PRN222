using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Services.Implementations;
using ServiceLayer.Services.Interfaces;

namespace PresentationLayer.Pages.Settings;

[Authorize(Roles = "Admin,Lecturer")]
public class IndexModel : PageModel
{
    private readonly ISystemSettingService _settingService;
    private readonly INotificationService _notifier;

    public IndexModel(ISystemSettingService settingService, INotificationService notifier)
    {
        _settingService = settingService;
        _notifier = notifier;
    }

    private string Actor => User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Quản trị viên";

    [BindProperty]
    public string ChunkingStrategy { get; set; } = "SemanticKernel";

    [BindProperty]
    public string EmbeddingModel { get; set; } = "Keyword";

    [BindProperty]
    public string OpenAIApiKey { get; set; } = "";

    [BindProperty]
    public string HuggingFaceApiToken { get; set; } = "";

    public bool HasOpenAIKey { get; private set; }
    public bool HasHuggingFaceToken { get; private set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Cài đặt RBL";
        ViewData["TopbarTitle"] = "⚙️ Cài đặt Thực nghiệm";

        ChunkingStrategy = await _settingService.GetSettingAsync("Rbl.ChunkingStrategy", "SemanticKernel");
        EmbeddingModel = await _settingService.GetSettingAsync("Rbl.EmbeddingModel", "Keyword");
        HasOpenAIKey = !string.IsNullOrEmpty(await _settingService.GetSettingAsync("Rbl.OpenAIApiKey", ""));
        HasHuggingFaceToken = !string.IsNullOrEmpty(await _settingService.GetSettingAsync("Rbl.HuggingFaceApiToken", ""));
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _settingService.SetSettingAsync("Rbl.ChunkingStrategy", ChunkingStrategy, "Chiến lược cắt chunk tài liệu");
        await _settingService.SetSettingAsync("Rbl.EmbeddingModel", EmbeddingModel, "Mô hình nhúng Vector");

        if (!string.IsNullOrWhiteSpace(OpenAIApiKey))
            await _settingService.SetSettingAsync("Rbl.OpenAIApiKey", OpenAIApiKey.Trim(), "OpenAI API Key");

        if (!string.IsNullOrWhiteSpace(HuggingFaceApiToken))
            await _settingService.SetSettingAsync("Rbl.HuggingFaceApiToken", HuggingFaceApiToken.Trim(), "HuggingFace API Token");

        await _notifier.ActivityAsync("⚙️", "Cài đặt", Actor,
            $"Đổi cấu hình RBL (chunk: {ChunkingStrategy} · embedding: {EmbeddingModel})");
        TempData["Success"] = "✅ Đã lưu cấu hình thực nghiệm thành công.";
        return RedirectToPage();
    }
}
