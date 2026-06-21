using DataAccessLayer.Entities;

namespace DataAccessLayer.Repositories;

public interface IDocumentRepository
{
    Task<List<Document>> GetBySubjectAsync(string subjectId);
    Task<List<Document>> GetByChapterAsync(string chapterId);
    Task<int> CountByChapterAsync(string chapterId);
    Task<List<Document>> GetAllAsync();
    Task<List<Document>> SearchAsync(string? subjectId, string? query);
    Task<Document?> GetByIdAsync(string id);
    Task<Document?> GetBySubjectAndHashAsync(string subjectId, string contentHash);
    Task<Document?> GetBySubjectAndFileNameAsync(string subjectId, string fileName);
    Task CreateAsync(Document document);
    Task UpdateAsync(Document document);
    Task DeleteAsync(string id);
}



