namespace ServiceLayer.Dtos;

public class EmbeddingComparisonResult
{
    public string Question { get; set; } = "";
    public string? SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public string ChunkingStrategy { get; set; } = "SemanticKernel";
    public int DocumentsUsed { get; set; }
    public int ChunksBuilt { get; set; }
    public List<EmbeddingModelResult> Models { get; set; } = new();
    public EmbeddingComparisonInsights Insights { get; set; } = new();
}

public class EmbeddingModelResult
{
    public string Model { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Description { get; set; } = "";
    public int EmbeddedChunks { get; set; }
    public int RetrievedCount { get; set; }
    public float TopScore { get; set; }
    public float AvgTopScore { get; set; }
    public string Answer { get; set; } = "";
    public List<ChunkHit> Hits { get; set; } = new();
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public long LatencyMs { get; set; }
    public long EmbedLatencyMs { get; set; }
    public decimal CostUsd { get; set; }
    public bool IsError { get; set; }
    public string? ErrorMessage { get; set; }
}

public class EmbeddingComparisonInsights
{
    public string WinnerRetrieval { get; set; } = "";
    public string WinnerEmbedLatency { get; set; } = "";
    public string WinnerE2ELatency { get; set; } = "";
    public List<string> Notes { get; set; } = new();
}
