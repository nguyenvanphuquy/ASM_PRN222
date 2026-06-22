namespace DataAccessLayer.Entities;

/// <summary>
/// Một chương (Chapter) thuộc về một môn học (Subject). Tài liệu (Document) được tổ chức theo
/// cấu trúc Subject → Chapter → Document — giống mô hình của Assignment 1 (LearningDocumentSystem).
/// </summary>
public class Chapter
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SubjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    // Thứ tự hiển thị các chương trong một môn (Chương 1, 2, 3...).
    public int OrderIndex { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public virtual Subject? Subject { get; set; }
}
