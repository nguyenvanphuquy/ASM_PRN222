using System;

namespace ServiceLayer.DTOs
{
    public class DocumentDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string SubjectId { get; set; } = string.Empty;
        public string? ChapterId { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public int ChunkCount { get; set; }
        public string Status { get; set; } = "Indexed";
        public DateTime UploadedAt { get; set; }
        public int QualityScore { get; set; }
        public string? QualitySummary { get; set; }
        public string? QualityWarnings { get; set; }
        public string? ExtractedText { get; set; }
    }
}

