using DataAccessLayer.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace PresentationLayer.Pages.AuditLogs;

/// <summary>
/// Nhật ký hệ thống — chỉ Admin. Đọc từ bảng SystemActivities (persist),
/// nên vẫn thấy thao tác xoá tài liệu / đăng nhập sau khi reload.
/// </summary>
[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ServiceLayer.Services.Interfaces.IUserService _users;
    private readonly ServiceLayer.Services.Interfaces.IDocumentService _docs;
    private readonly ServiceLayer.Services.Interfaces.IFeedbackService _feedback;
    private readonly ServiceLayer.Services.Interfaces.IBillingService _billing;

    public IndexModel(
        AppDbContext db,
        ServiceLayer.Services.Interfaces.IUserService users,
        ServiceLayer.Services.Interfaces.IDocumentService docs,
        ServiceLayer.Services.Interfaces.IFeedbackService feedback,
        ServiceLayer.Services.Interfaces.IBillingService billing)
    {
        _db = db;
        _users = users;
        _docs = docs;
        _feedback = feedback;
        _billing = billing;
    }

    public record AuditEntry(DateTime Time, string Icon, string Category, string Actor, string Description);

    public List<AuditEntry> Entries { get; private set; } = [];

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Nhật ký hệ thống";
        ViewData["TopbarTitle"] = "📜 Nhật ký hệ thống";

        await EnsureBackfillAsync();

        Entries = await _db.SystemActivities
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Take(500)
            .Select(a => new AuditEntry(a.CreatedAt, a.Icon, a.Category, a.Actor, a.Description))
            .ToListAsync();
    }

    /// <summary>
    /// Lần đầu (bảng trống): seed từ dữ liệu đang có để không mất lịch sử cũ.
    /// Các sự kiện mới (gồm xoá tài liệu) ghi qua ActivityAsync.
    /// </summary>
    private async Task EnsureBackfillAsync()
    {
        if (await _db.SystemActivities.AnyAsync()) return;

        var seed = new List<DataAccessLayer.Entities.SystemActivity>();

        var users = await _users.GetAllAsync();
        var userMap = users.ToDictionary(u => u.Id, u => u.FullName);
        foreach (var u in users)
        {
            seed.Add(new()
            {
                Icon = "👤",
                Category = "Tài khoản",
                Actor = u.FullName,
                Description = $"Tài khoản {u.Role} \"{u.Username}\" được tạo",
                CreatedAt = u.CreatedAt
            });
        }

        var docs = await _docs.GetAllAsync();
        foreach (var d in docs)
        {
            var actor = userMap.TryGetValue(d.UploadedBy, out var name) ? name : d.UploadedBy;
            seed.Add(new()
            {
                Icon = "📄",
                Category = "Tài liệu",
                Actor = actor,
                Description = $"Tải lên tài liệu \"{d.Title}\" ({d.Status})",
                CreatedAt = d.UploadedAt
            });
        }

        var feedback = await _feedback.GetAllAsync();
        foreach (var f in feedback)
        {
            seed.Add(new()
            {
                Icon = "💡",
                Category = "Phản hồi",
                Actor = f.UserName,
                Description = $"Gửi phản hồi ({f.Rating}★): {Truncate(f.Content, 60)}",
                CreatedAt = f.CreatedAt
            });
        }

        var purchases = await _billing.GetAllPurchasesAsync();
        foreach (var p in purchases)
        {
            var actor = userMap.TryGetValue(p.UserId, out var name) ? name : p.UserId;
            var actionText = p.Status == "Cancelled" ? "Mua & hủy gói" : "Mua gói";
            var icon = p.Status == "Cancelled" ? "🚫" : "🛒";
            seed.Add(new()
            {
                Icon = icon,
                Category = "Giao dịch",
                Actor = actor,
                Description = $"{actionText} \"{p.PackageName}\" ({p.TokensGranted:N0} token)",
                CreatedAt = p.CreatedAt
            });
        }

        if (seed.Count == 0) return;
        _db.SystemActivities.AddRange(seed);
        await _db.SaveChangesAsync();
    }

    private static string Truncate(string s, int n)
        => string.IsNullOrEmpty(s) || s.Length <= n ? s : s[..n] + "…";
}
