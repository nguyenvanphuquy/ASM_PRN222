namespace DataAccessLayer.Entities;

public class Subject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;

    // Navigation Properties
    public virtual ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
}
