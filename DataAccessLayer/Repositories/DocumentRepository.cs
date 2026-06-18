using DataAccessLayer.Context;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly AppDbContext _context;
    public DocumentRepository(AppDbContext context) => _context = context;

    public Task<List<Document>> GetBySubjectAsync(string subjectId)
        => _context.Documents.Where(d => d.SubjectId == subjectId)
            .OrderByDescending(d => d.UploadedAt).ToListAsync();

    public Task<List<Document>> GetByChapterAsync(string chapterId)
        => _context.Documents.Where(d => d.ChapterId == chapterId)
            .OrderByDescending(d => d.UploadedAt).ToListAsync();

    public async Task<int> CountByChapterAsync(string chapterId)
        => await _context.Documents.CountAsync(d => d.ChapterId == chapterId);

    public Task<List<Document>> GetAllAsync()
        => _context.Documents.OrderByDescending(d => d.UploadedAt).ToListAsync();

    public Task<List<Document>> SearchAsync(string? subjectId, string? query)
    {
        var docs = _context.Documents.AsQueryable();

        if (!string.IsNullOrWhiteSpace(subjectId))
            docs = docs.Where(d => d.SubjectId == subjectId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            // Match against subject (name/code) so users can search by môn học, plus document title/filename.
            var matchedSubjectIds = _context.Subjects
                .Where(s => s.Name.Contains(q) || s.Code.Contains(q))
                .Select(s => s.Id);

            docs = docs.Where(d =>
                d.Title.Contains(q) ||
                d.FileName.Contains(q) ||
                matchedSubjectIds.Contains(d.SubjectId));
        }

        return docs.OrderByDescending(d => d.UploadedAt).ToListAsync();
    }

    public Task<Document?> GetByIdAsync(string id)
        => _context.Documents.FirstOrDefaultAsync(d => d.Id == id);

    public Task<Document?> GetBySubjectAndHashAsync(string subjectId, string contentHash)
        => _context.Documents.FirstOrDefaultAsync(d => d.SubjectId == subjectId && d.ContentHash == contentHash);

    public Task<Document?> GetBySubjectAndFileNameAsync(string subjectId, string fileName)
        => _context.Documents
            .Where(d => d.SubjectId == subjectId && d.FileName == fileName)
            .OrderByDescending(d => d.UploadedAt)
            .FirstOrDefaultAsync();

    public async Task CreateAsync(Document document)
    {
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Document document)
    {
        _context.Documents.Update(document);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id)
    {
        var document = await _context.Documents.FindAsync(id);
        if (document != null)
        {
            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();
        }
    }
}
