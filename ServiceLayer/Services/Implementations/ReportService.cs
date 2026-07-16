using DataAccessLayer.Repositories.Interfaces;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;
using ServiceLayer.Settings;

namespace ServiceLayer.Services.Implementations;

public class ReportService : IReportService
{
    private readonly IBillingRepository _billing;
    private readonly IUserRepository _users;

    public ReportService(IBillingRepository billing, IUserRepository users)
    {
        _billing = billing;
        _users = users;
    }

    public async Task<TokenReport> GetTokenReportAsync(string range)
    {
        var since = range switch
        {
            "week" => DateTime.UtcNow.AddDays(-7),
            "all" => DateTime.MinValue,
            _ => DateTime.UtcNow.AddDays(-30),
        };

        var logs = await _billing.GetUsageSinceAsync(since);
        var users = await _users.GetAllAsync();
        var userMap = users.ToDictionary(u => u.Id, u => u);

        var report = new TokenReport
        {
            Range = range,
            TotalTokens = logs.Sum(l => (long)l.TotalTokens),
            TotalPrompt = logs.Sum(l => (long)l.PromptTokens),
            TotalCompletion = logs.Sum(l => (long)l.CompletionTokens),
            TotalRequests = logs.Count,
            TotalCostUsd = logs.Sum(l => l.CostUsd),
            // Tách riêng phần CHAT (vận hành) khỏi benchmark RBL để tính chi phí/lợi nhuận cho đúng.
            ChatTokens = logs.Where(l => l.Kind == "chat").Sum(l => (long)l.TotalTokens),
            ChatCostUsd = logs.Where(l => l.Kind == "chat").Sum(l => l.CostUsd),
            ActiveUsers = logs.Select(l => l.UserId).Distinct().Count(),
        };

        report.ByUser = logs
            .GroupBy(l => l.UserId)
            .Select(g =>
            {
                userMap.TryGetValue(g.Key, out var u);
                return new UserTokenStat
                {
                    UserId = g.Key,
                    UserName = u?.FullName ?? u?.Username ?? "(đã xoá)",
                    Role = u?.Role ?? "-",
                    Tokens = g.Sum(x => (long)x.TotalTokens),
                    Requests = g.Count(),
                    CostUsd = g.Sum(x => x.CostUsd),
                };
            })
            .OrderByDescending(x => x.Tokens)
            .ToList();

        report.ByModel = logs
            .GroupBy(l => l.Model)
            .Select(g => new ModelTokenStat
            {
                Model = g.Key,
                DisplayName = ModelCatalog.Get(g.Key).DisplayName,
                Tokens = g.Sum(x => (long)x.TotalTokens),
                Requests = g.Count(),
                CostUsd = g.Sum(x => x.CostUsd),
            })
            .OrderByDescending(x => x.Tokens)
            .ToList();

        // Chuỗi thời gian: 14 ngày gần nhất (theo giờ địa phương).
        int days = range == "week" ? 7 : 14;
        var byDay = logs
            .GroupBy(l => l.CreatedAt.ToLocalTime().Date)
            .ToDictionary(g => g.Key, g => g.Sum(x => (long)x.TotalTokens));

        var today = DateTime.Now.Date;
        for (int i = days - 1; i >= 0; i--)
        {
            var d = today.AddDays(-i);
            report.Daily.Add(new DailyTokenPoint { Date = d, Tokens = byDay.GetValueOrDefault(d, 0) });
        }

        return report;
    }

    public async Task<RevenueReport> GetRevenueReportAsync(string range)
    {
        var since = range switch
        {
            "week" => DateTime.UtcNow.AddDays(-7),
            "all" => DateTime.MinValue,
            _ => DateTime.UtcNow.AddDays(-30),
        };

        var purchases = await _billing.GetAllPurchasesAsync();
        // "Lượt mua gói" & doanh thu CHỈ tính giao dịch có trả tiền (AmountVnd > 0) TRONG KHOẢNG range:
        //  - Bỏ gói dùng thử miễn phí (auto-grant 0đ) — đó không phải "lượt mua".
        //  - Gói đã HỦY vẫn tính (app không hoàn tiền → tiền đã thu vẫn là doanh thu thật).
        var sales = purchases.Where(p => p.AmountVnd > 0 && p.CreatedAt >= since).ToList();
        var usage = await _billing.GetUsageSinceAsync(since);
        var users = await _users.GetAllAsync();
        var userMap = users.ToDictionary(u => u.Id, u => u);

        // Lợi nhuận chỉ trừ chi phí VẬN HÀNH (chat); chi phí benchmark RBL để riêng.
        decimal chatCostUsd = usage.Where(l => l.Kind == "chat").Sum(l => l.CostUsd);
        long chatCostVnd = ModelCatalog.ToVnd(chatCostUsd);
        long researchCostVnd = ModelCatalog.ToVnd(usage.Where(l => l.Kind != "chat").Sum(l => l.CostUsd));
        long revenue = sales.Sum(p => p.AmountVnd);

        var report = new RevenueReport
        {
            Range = range,
            TotalRevenueVnd = revenue,
            TotalPurchases = sales.Count,
            PayingUsers = sales.Select(p => p.UserId).Distinct().Count(),
            TokensSold = sales.Sum(p => (long)p.TokensGranted),
            TokensUsed = sales.Sum(p => (long)p.TokensUsed),
            EstimatedCostUsd = chatCostUsd,
            EstimatedCostVnd = chatCostVnd,
            ResearchCostVnd = researchCostVnd,
            ProfitVnd = revenue - chatCostVnd,
        };

        report.ByPackage = sales
            .GroupBy(p => p.PackageName)
            .Select(g => new PackageRevenueStat
            {
                PackageName = g.Key,
                Count = g.Count(),
                RevenueVnd = g.Sum(x => x.AmountVnd),
                TokensSold = g.Sum(x => (long)x.TokensGranted),
                TokensUsed = g.Sum(x => (long)x.TokensUsed),
            })
            .OrderByDescending(x => x.RevenueVnd)
            .ToList();

        report.Recent = purchases
            .Where(p => p.CreatedAt >= since)
            .Take(12)
            .Select(p =>
            {
                userMap.TryGetValue(p.UserId, out var u);
                return new RecentPurchase
                {
                    UserName = u?.FullName ?? u?.Username ?? "(đã xoá)",
                    PackageName = p.PackageName,
                    AmountVnd = p.AmountVnd,
                    CreatedAt = p.CreatedAt,
                    Status = p.Status,
                };
            })
            .ToList();

        return report;
    }
}
