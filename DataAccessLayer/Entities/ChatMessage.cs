namespace DataAccessLayer.Entities;

public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
    public List<ChatSource> Sources { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ChatSource
{
    public string DocumentId { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int Page { get; set; }
    public string Snippet { get; set; } = string.Empty;
}
