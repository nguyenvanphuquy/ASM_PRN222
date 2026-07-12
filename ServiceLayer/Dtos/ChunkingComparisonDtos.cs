namespace ServiceLayer.Dtos;

public class ChunkingComparisonResult
{
    public string Question { get; set; } = "";
    public string? SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public string EmbeddingMode { get; set; } = "Keyword";
    public int DocumentsUsed { get; set; }
    public List<ChunkingStrategyResult> Strategies { get; set; } = new();
    public ChunkingComparisonInsights Insights { get; set; } = new();
}

public class ChunkingStrategyResult
{
    public string Strategy { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public int TotalChunks { get; set; }
    public int AvgChunkLength { get; set; }
    public int RetrievedCount { get; set; }
    public float TopScore { get; set; }
    public float AvgTopScore { get; set; }
    public string Answer { get; set; } = "";
    public List<ChunkHit> Hits { get; set; } = new();
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public long LatencyMs { get; set; }
    public decimal CostUsd { get; set; }
    public bool IsError { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ChunkHit
{
    public string DocumentName { get; set; } = "";
    public int ChunkIndex { get; set; }
    public int Page { get; set; }
    public float Score { get; set; }
    public string Snippet { get; set; } = "";
}

public class ChunkingComparisonInsights
{
    public string WinnerRetrieval { get; set; } = "";
    public string WinnerLatency { get; set; } = "";
    public string WinnerChunkDensity { get; set; } = "";
    public List<string> Notes { get; set; } = new();
}
