using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.Services.Interfaces;

namespace PresentationLayer.Pages.AuditLogs;

/// <summary>
/// Nhật ký hệ thống — chỉ Admin. Tổng hợp các hoạt động gần đây của hệ thống
/// (tạo tài khoản, tải tài liệu, gửi phản hồi) từ dữ liệu sẵn có để giám sát.
/// </summary>
[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly IUserService _users;
    private readonly IDocumentService _docs;
    private readonly IFeedbackService _feedback;

    public IndexModel(IUserService users, IDocumentService docs, IFeedbackService feedback)
    {
        _users = users;
        _docs = docs;
        _feedback = feedback;
    }

    public record AuditEntry(DateTime Time, string Icon, string Category, string Actor, string Description);

    public List<AuditEntry> Entries { get; private set; } = [];

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Nhật ký hệ thống";
        ViewData["TopbarTitle"] = "📜 Nhật ký hệ thống";

        var entries = new List<AuditEntry>();

        var users = await _users.GetAllAsync();
        var userMap = users.ToDictionary(u => u.Id, u => u.FullName);
        foreach (var u in users)
        {
            entries.Add(new AuditEntry(
                u.CreatedAt, "👤", "Tài khoản", u.FullName,
                $"Tài khoản {u.Role} \"{u.Username}\" được tạo"));
        }

        var docs = await _docs.GetAllAsync();
        foreach (var d in docs)
        {
            var actor = userMap.TryGetValue(d.UploadedBy, out var name) ? name : d.UploadedBy;
            entries.Add(new AuditEntry(
                d.UploadedAt, "📄", "Tài liệu", actor,
                $"Tải lên tài liệu \"{d.Title}\" ({d.Status})"));
        }

        var feedback = await _feedback.GetAllAsync();
        foreach (var f in feedback)
        {
            entries.Add(new AuditEntry(
                f.CreatedAt, "💡", "Phản hồi", f.UserName,
                $"Gửi phản hồi ({f.Rating}★): {Truncate(f.Content, 60)}"));
        }

        Entries = entries.OrderByDescending(e => e.Time).ToList();
    }

    private static string Truncate(string s, int n)
        => string.IsNullOrEmpty(s) || s.Length <= n ? s : s[..n] + "…";
}
