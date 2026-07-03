namespace DataAccessLayer.Entities;

public class FeedbackReply
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FeedbackId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? UserAvatar { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public virtual Feedback? Feedback { get; set; }
    public virtual User? User { get; set; }
}
