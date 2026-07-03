using DataAccessLayer.Entities;

namespace DataAccessLayer.Repositories.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByIdAsync(string id);
    Task<User?> GetByVerificationTokenAsync(string token);
    Task<List<User>> GetAllAsync();
    Task CreateAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(string id);
    Task<long> CountAsync();
    Task<long> CountByRoleAsync(string role);

    // ── Phân công giảng viên ↔ môn học (nhiều môn / 1 giảng viên; 1 môn / 1 giảng viên) ──
    Task<List<LecturerSubject>> GetAllLecturerSubjectsAsync();
    Task<List<string>> GetAssignedSubjectIdsAsync(string userId);
    /// <summary>Thay toàn bộ danh sách môn được giao cho một giảng viên (xoá cũ, thêm mới).</summary>
    Task ReplaceAssignedSubjectsAsync(string userId, IEnumerable<string> subjectIds);
}



