using DataAccessLayer.Entities;

namespace ServiceLayer.Dtos;

/// <summary>Kết quả so sánh hai hướng tiếp cận: RAG vs Fine-tuned (mô phỏng parametric).</summary>
public class RagVsFineTunedResult
{
    public string Question { get; set; } = "";
    public string? SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public string Model { get; set; } = "";
    public int ContextChunks { get; set; }
    public ApproachAnswer Rag { get; set; } = new();
    public ApproachAnswer FineTuned { get; set; } = new();
    public ComparisonInsights Insights { get; set; } = new();
}

public class ApproachAnswer
{
    public string Approach { get; set; } = "";       // "RAG" | "FineTuned"
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    public string Answer { get; set; } = "";
    public List<ChatSource> Sources { get; set; } = new();
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public long LatencyMs { get; set; }
    public decimal CostUsd { get; set; }
    public bool IsError { get; set; }
    public bool HasCitations => Sources.Count > 0;
    public bool GroundedInDocuments { get; set; }
}

public class ComparisonInsights
{
    public string WinnerLatency { get; set; } = "";
    public string WinnerTokens { get; set; } = "";
    public string WinnerGroundedness { get; set; } = "";
    public List<string> Notes { get; set; } = new();
}
