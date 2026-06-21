using DataAccessLayer.Entities;
using DataAccessLayer.Repositories;

namespace ServiceLayer.Services;

public class ChapterService : IChapterService
{
    private readonly IChapterRepository _repo;
    private readonly AutoMapper.IMapper _mapper;
    private readonly IDocumentRepository _docRepo;

    public ChapterService(IChapterRepository repo, IDocumentRepository docRepo, AutoMapper.IMapper mapper)
    {
        _repo = repo;
        _docRepo = docRepo;
        _mapper = mapper;
    }

    public async Task<List<ServiceLayer.DTOs.ChapterDto>> GetBySubjectAsync(string subjectId) { var entities = await _repo.GetBySubjectAsync(subjectId); return _mapper.Map<List<ServiceLayer.DTOs.ChapterDto>>(entities); }
    public async Task<List<ServiceLayer.DTOs.ChapterDto>> GetAllAsync() { var entities = await _repo.GetAllAsync(); return _mapper.Map<List<ServiceLayer.DTOs.ChapterDto>>(entities); }
    public Task<Chapter?> GetByIdAsync(string id) => _repo.GetByIdAsync(id);

    public async Task<Chapter> CreateAsync(string subjectId, string title, string description, int orderIndex)
    {
        // Mặc định OrderIndex = số chương hiện có + 1 nếu người dùng không nhập (<= 0).
        if (orderIndex <= 0)
            orderIndex = await _repo.CountBySubjectAsync(subjectId) + 1;

        var chapter = new Chapter
        {
            SubjectId = subjectId,
            Title = title.Trim(),
            Description = description?.Trim() ?? string.Empty,
            OrderIndex = orderIndex
        };
        await _repo.CreateAsync(chapter);
        return chapter;
    }

    public async Task UpdateAsync(string id, string title, string description, int orderIndex)
    {
        var chapter = await _repo.GetByIdAsync(id);
        if (chapter == null) return;

        chapter.Title = title.Trim();
        chapter.Description = description?.Trim() ?? string.Empty;
        if (orderIndex > 0) chapter.OrderIndex = orderIndex;
        await _repo.UpdateAsync(chapter);
    }

    public async Task DeleteAsync(string id)
    {
        // Gỡ liên kết các tài liệu đang thuộc chương này trước khi xoá chương (không xoá tài liệu).
        var docs = await _docRepo.GetByChapterAsync(id);
        foreach (var d in docs)
        {
            d.ChapterId = null;
            await _docRepo.UpdateAsync(d);
        }
        await _repo.DeleteAsync(id);
    }
}





