using DataAccessLayer.Repositories.Interfaces;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;
using ServiceLayer.Settings;

namespace ServiceLayer.Services.Implementations;

public class ReportService : IReportService
{
    // Tỷ giá quy đổi để ước tính chi phí LLM ra VND (dùng cho tính lợi nhuận gói).
    private const decimal UsdToVnd = 25_000m;

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

    public async Task<RevenueReport> GetRevenueReportAsync()
    {
        var purchases = await _billing.GetAllPurchasesAsync();
        var paid = purchases.Where(p => p.Status == "Paid").ToList();
        var usage = await _billing.GetUsageSinceAsync(DateTime.MinValue);
        var users = await _users.GetAllAsync();
        var userMap = users.ToDictionary(u => u.Id, u => u);

        decimal costUsd = usage.Sum(l => l.CostUsd);
        long costVnd = (long)(costUsd * UsdToVnd);
        long revenue = paid.Sum(p => p.AmountVnd);

        var report = new RevenueReport
        {
            TotalRevenueVnd = revenue,
            TotalPurchases = paid.Count,
            PayingUsers = paid.Where(p => p.AmountVnd > 0).Select(p => p.UserId).Distinct().Count(),
            TokensSold = paid.Sum(p => (long)p.TokensGranted),
            TokensUsed = paid.Sum(p => (long)p.TokensUsed),
            EstimatedCostUsd = costUsd,
            EstimatedCostVnd = costVnd,
            ProfitVnd = revenue - costVnd,
        };

        report.ByPackage = paid
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
