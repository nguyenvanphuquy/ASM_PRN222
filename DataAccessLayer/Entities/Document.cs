namespace DataAccessLayer.Entities;

public class Document
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    // Chương (Chapter) mà tài liệu thuộc về — tuỳ chọn (null = chưa gán chương).
    public string? ChapterId { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int ChunkCount { get; set; }
    public string Status { get; set; } = "Indexed";
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public string? ExtractedText { get; set; }
    public int QualityScore { get; set; }
    public string? QualitySummary { get; set; }
    public string? QualityWarnings { get; set; }

    // Navigation Properties
    public virtual Subject? Subject { get; set; }
    public virtual Chapter? Chapter { get; set; }
    public virtual User? Uploader { get; set; }
}
