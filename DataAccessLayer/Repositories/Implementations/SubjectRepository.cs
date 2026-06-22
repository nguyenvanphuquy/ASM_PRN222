using DataAccessLayer.Context;
using DataAccessLayer.Entities;
using DataAccessLayer.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories;

public class SubjectRepository : ISubjectRepository
{
    private readonly AppDbContext _context;
    public SubjectRepository(AppDbContext context) => _context = context;

    public Task<List<Subject>> GetAllAsync()
        => _context.Subjects.OrderBy(s => s.Code).ToListAsync();

    public Task<Subject?> GetByIdAsync(string id)
        => _context.Subjects.FirstOrDefaultAsync(s => s.Id == id);

    public async Task CreateAsync(Subject subject)
    {
        _context.Subjects.Add(subject);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Subject subject)
    {
        _context.Subjects.Update(subject);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id)
    {
        var subject = await _context.Subjects.FindAsync(id);
        if (subject != null)
        {
            subject.IsDeleted = true;
            
            // Gỡ môn học khỏi các Giảng viên đang được phân công
            var users = await _context.Users.Where(u => u.AssignedSubjectId == id).ToListAsync();
            foreach (var u in users)
            {
                u.AssignedSubjectId = null;
            }

            await _context.SaveChangesAsync();
        }
    }

    public async Task<long> CountAsync() => await _context.Subjects.LongCountAsync();
}


