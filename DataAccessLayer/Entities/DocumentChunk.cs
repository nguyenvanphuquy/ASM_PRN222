namespace DataAccessLayer.Entities;

public class DocumentChunk
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DocumentId { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public int Page { get; set; }
    public string? VectorJson { get; set; }
    public string? EmbeddingModel { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
