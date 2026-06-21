using System;

namespace ServiceLayer.DTOs
{
    public class DocumentChunkDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string DocumentId { get; set; } = string.Empty;
        public string DocumentName { get; set; } = string.Empty;
        public string SubjectId { get; set; } = string.Empty;
        public int ChunkIndex { get; set; }
        public string Content { get; set; } = string.Empty;
        public int Page { get; set; }
        public int TokenCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
