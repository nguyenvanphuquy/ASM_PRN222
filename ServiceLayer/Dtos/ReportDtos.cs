namespace ServiceLayer.Dtos;

// ─────────────────────────── Token usage report ───────────────────────────

public class TokenReport
{
    public string Range { get; set; } = "month";          // week | month | all
    public long TotalTokens { get; set; }
    public long TotalPrompt { get; set; }
    public long TotalCompletion { get; set; }
    public int TotalRequests { get; set; }
    public decimal TotalCostUsd { get; set; }
    public int ActiveUsers { get; set; }

    public List<UserTokenStat> ByUser { get; set; } = new();
    public List<ModelTokenStat> ByModel { get; set; } = new();
    public List<DailyTokenPoint> Daily { get; set; } = new();
}

public class UserTokenStat
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Role { get; set; } = "";
    public long Tokens { get; set; }
    public int Requests { get; set; }
    public decimal CostUsd { get; set; }
}

public class ModelTokenStat
{
    public string Model { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public long Tokens { get; set; }
    public int Requests { get; set; }
    public decimal CostUsd { get; set; }
}

public class DailyTokenPoint
{
    public DateTime Date { get; set; }
    public long Tokens { get; set; }
}

// ─────────────────────────── Package revenue report ───────────────────────────

public class RevenueReport
{
    public long TotalRevenueVnd { get; set; }
    public int TotalPurchases { get; set; }
    public int PayingUsers { get; set; }
    public long TokensSold { get; set; }
    public long TokensUsed { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public long EstimatedCostVnd { get; set; }
    public long ProfitVnd { get; set; }

    public List<PackageRevenueStat> ByPackage { get; set; } = new();
    public List<RecentPurchase> Recent { get; set; } = new();
}

public class PackageRevenueStat
{
    public string PackageName { get; set; } = "";
    public int Count { get; set; }
    public long RevenueVnd { get; set; }
    public long TokensSold { get; set; }
    public long TokensUsed { get; set; }
}

public class RecentPurchase
{
    public string UserName { get; set; } = "";
    public string PackageName { get; set; } = "";
    public long AmountVnd { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = "";
}

// ─────────────────────────── Model comparison (benchmark) ───────────────────────────

public class ModelComparisonResult
{
    public string Question { get; set; } = "";
    public int ContextChunks { get; set; }
    public List<ModelAnswer> Answers { get; set; } = new();
}

public class ModelAnswer
{
    public string Model { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Vendor { get; set; } = "";
    public string Answer { get; set; } = "";
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public long LatencyMs { get; set; }
    public decimal CostUsd { get; set; }
    public bool IsError { get; set; }
}
