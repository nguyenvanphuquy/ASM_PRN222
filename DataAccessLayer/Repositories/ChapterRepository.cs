using DataAccessLayer.Context;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories;

public class ChapterRepository : IChapterRepository
{
    private readonly AppDbContext _context;
    public ChapterRepository(AppDbContext context) => _context = context;

    public Task<List<Chapter>> GetBySubjectAsync(string subjectId)
        => _context.Chapters.Where(c => c.SubjectId == subjectId)
            .OrderBy(c => c.OrderIndex).ThenBy(c => c.CreatedAt).ToListAsync();

    public Task<List<Chapter>> GetAllAsync()
        => _context.Chapters.OrderBy(c => c.SubjectId).ThenBy(c => c.OrderIndex).ToListAsync();

    public Task<Chapter?> GetByIdAsync(string id)
        => _context.Chapters.FirstOrDefaultAsync(c => c.Id == id);

    public async Task CreateAsync(Chapter chapter)
    {
        _context.Chapters.Add(chapter);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Chapter chapter)
    {
        _context.Chapters.Update(chapter);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id)
    {
        var chapter = await _context.Chapters.FindAsync(id);
        if (chapter != null)
        {
            _context.Chapters.Remove(chapter);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> CountBySubjectAsync(string subjectId)
        => await _context.Chapters.CountAsync(c => c.SubjectId == subjectId);
}
