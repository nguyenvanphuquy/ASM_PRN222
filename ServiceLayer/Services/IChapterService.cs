using DataAccessLayer.Entities;

namespace ServiceLayer.Services;

public interface IChapterService
{
    Task<List<ServiceLayer.DTOs.ChapterDto>> GetBySubjectAsync(string subjectId);
    Task<List<ServiceLayer.DTOs.ChapterDto>> GetAllAsync();
    Task<Chapter?> GetByIdAsync(string id);
    Task<Chapter> CreateAsync(string subjectId, string title, string description, int orderIndex);
    Task UpdateAsync(string id, string title, string description, int orderIndex);
    // Xoá chương. Tài liệu thuộc chương sẽ được gỡ liên kết (ChapterId = null), KHÔNG bị xoá.
    Task DeleteAsync(string id);
}



