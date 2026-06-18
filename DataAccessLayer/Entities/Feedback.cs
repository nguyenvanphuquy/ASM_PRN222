namespace DataAccessLayer.Entities;

public class Feedback
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? UserAvatar { get; set; }
    public int Rating { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Admin reply
    public string? AdminReply { get; set; }
    public string? RepliedBy { get; set; }
    public string? RepliedByAvatar { get; set; }
    public DateTime? RepliedAt { get; set; }
}
