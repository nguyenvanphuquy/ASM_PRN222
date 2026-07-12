namespace DataAccessLayer.Entities;

/// <summary>Một lần chạy benchmark RBL (chunking / embedding / rag-vs-ft).</summary>
public class ExperimentRun
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    /// <summary>chunking | embedding | rag-vs-ft</summary>
    public string Kind { get; set; } = "";
    public string Question { get; set; } = "";
    public string? SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public string UserId { get; set; } = "";
    public string? WinnerLabel { get; set; }
    public string? NotesJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<ExperimentVariant> Variants { get; set; } = new List<ExperimentVariant>();
}

/// <summary>Một biến thể trong lần chạy (strategy / embedding model / RAG|FT).</summary>
public class ExperimentVariant
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ExperimentRunId { get; set; } = "";
    public string VariantKey { get; set; } = "";
    public string VariantLabel { get; set; } = "";
    public float Score { get; set; }
    public long LatencyMs { get; set; }
    public int TotalTokens { get; set; }
    public int ExtraInt { get; set; } // chunk count / embedded count / sources
    public bool IsError { get; set; }
    public string? AnswerPreview { get; set; }

    public virtual ExperimentRun? Run { get; set; }
}
