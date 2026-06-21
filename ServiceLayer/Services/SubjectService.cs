using DataAccessLayer.Entities;
using DataAccessLayer.Repositories;

namespace ServiceLayer.Services;

public class SubjectService : ISubjectService
{
    private readonly ISubjectRepository _repo;
    private readonly AutoMapper.IMapper _mapper;
    public SubjectService(ISubjectRepository repo, AutoMapper.IMapper mapper)
    {
        _repo = repo;
        _mapper = mapper;
    }

    public async Task<List<ServiceLayer.DTOs.SubjectDto>> GetAllAsync() { var entities = await _repo.GetAllAsync(); return _mapper.Map<List<ServiceLayer.DTOs.SubjectDto>>(entities); }
    public async Task<ServiceLayer.DTOs.SubjectDto?> GetByIdAsync(string id) { var entity = await _repo.GetByIdAsync(id); return _mapper.Map<ServiceLayer.DTOs.SubjectDto>(entity); }

    public Task CreateAsync(string code, string name, string description)
        => _repo.CreateAsync(new Subject { Code = code.Trim(), Name = name.Trim(), Description = description?.Trim() ?? string.Empty });

    public async Task UpdateAsync(string id, string code, string name, string description)
    {
        var subject = await _repo.GetByIdAsync(id);
        if (subject == null) return;

        subject.Code = code.Trim();
        subject.Name = name.Trim();
        subject.Description = description?.Trim() ?? string.Empty;
        await _repo.UpdateAsync(subject);
    }

    public Task DeleteAsync(string id) => _repo.DeleteAsync(id);

    public async Task EnsureSeedAsync()
    {
        if (await _repo.CountAsync() > 0) return;
        // Seed đủ các môn mà bộ tài liệu mẫu + test set 50 câu tham chiếu tới.
        await _repo.CreateAsync(new Subject { Code = "PRN222", Name = "Advanced Cross Platform Application Programming", Description = "Môn học ASP.NET Core MVC tại FPT University." });
        await _repo.CreateAsync(new Subject { Code = "DBI202", Name = "Introduction to Databases", Description = "Cơ sở dữ liệu quan hệ và SQL." });
        await _repo.CreateAsync(new Subject { Code = "SWE301", Name = "Software Testing", Description = "Các phương pháp và quy trình kiểm thử phần mềm." });
        await _repo.CreateAsync(new Subject { Code = "OSG202", Name = "Operating Systems", Description = "Nguyên lý hệ điều hành: tiến trình, luồng và bộ nhớ." });
    }
}




