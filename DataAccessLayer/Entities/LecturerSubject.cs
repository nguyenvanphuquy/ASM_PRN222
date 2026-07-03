namespace DataAccessLayer.Entities;

/// <summary>
/// Bảng nối phân công giảng viên ↔ môn học.
/// Một giảng viên có thể phụ trách NHIỀU môn (upload tài liệu cho nhiều môn),
/// nhưng mỗi môn chỉ được giao cho ĐÚNG MỘT giảng viên (unique index trên SubjectId).
/// </summary>
public class LecturerSubject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
