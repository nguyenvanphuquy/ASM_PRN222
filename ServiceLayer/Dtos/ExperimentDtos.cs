namespace ServiceLayer.Dtos;

public class ExperimentDashboardDto
{
    public int TotalRuns { get; set; }
    public int ChunkingRuns { get; set; }
    public int EmbeddingRuns { get; set; }
    public int RagVsFtRuns { get; set; }
    public List<ExperimentRunDto> RecentRuns { get; set; } = new();
    public List<VariantAggDto> ChunkingAgg { get; set; } = new();
    public List<VariantAggDto> EmbeddingAgg { get; set; } = new();
    public List<VariantAggDto> RagVsFtAgg { get; set; } = new();
}

public class ExperimentRunDto
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string KindLabel { get; set; } = "";
    public string Question { get; set; } = "";
    public string? SubjectName { get; set; }
    public string? WinnerLabel { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ExperimentVariantDto> Variants { get; set; } = new();
}

public class ExperimentVariantDto
{
    public string VariantKey { get; set; } = "";
    public string VariantLabel { get; set; } = "";
    public float Score { get; set; }
    public long LatencyMs { get; set; }
    public int TotalTokens { get; set; }
    public int ExtraInt { get; set; }
    public bool IsError { get; set; }
    public string? AnswerPreview { get; set; }
}

public class VariantAggDto
{
    public string VariantLabel { get; set; } = "";
    public double AvgScore { get; set; }
    public double AvgLatencyMs { get; set; }
    public int Runs { get; set; }
}
