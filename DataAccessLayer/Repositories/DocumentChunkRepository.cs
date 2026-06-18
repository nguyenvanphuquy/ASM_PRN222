using System.Text.RegularExpressions;
using DataAccessLayer.Context;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories;

public class DocumentChunkRepository : IDocumentChunkRepository
{
    private readonly AppDbContext _context;
    public DocumentChunkRepository(AppDbContext context) => _context = context;

    // Từ thừa tiếng Việt không dấu — bỏ đi để không làm nhiễu việc tìm theo từ khóa
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "tom", "tat", "cho", "toi", "tai", "lieu", "cua", "mon", "gium", "giup",
        "hay", "nhe", "voi", "nay", "minh", "ban", "oi", "duoc", "the", "nao",
        "lai", "di", "va", "cac", "mot", "nhung", "trong", "ve", "la", "co",
        "khong", "hoac", "thi", "se", "day", "dum", "list", "ke", "ra", "noi",
        "summarize", "summary", "about", "the", "please", "give", "show"
    };

    public async Task InsertManyAsync(IEnumerable<DocumentChunk> chunks)
    {
        _context.DocumentChunks.AddRange(chunks);
        await _context.SaveChangesAsync();
    }

    public async Task<List<DocumentChunk>> SearchAsync(string query, string? subjectId, int limit)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<DocumentChunk>();

        var baseQuery = _context.DocumentChunks.AsQueryable();
        if (!string.IsNullOrEmpty(subjectId))
            baseQuery = baseQuery.Where(c => c.SubjectId == subjectId);

        // 1) Nếu người dùng nhắc tới "chương N / chapter N / bài N / buổi N" → ưu tiên
        //    lấy đúng tài liệu có số đó trong tên (vd: "chuong 1" → "Chapter 01 ...").
        var chapterNo = ExtractChapterNumber(query);
        if (chapterNo.HasValue)
        {
            var n = chapterNo.Value;
            var padded = n.ToString("D2");           // 1 → "01"
            var plain = n.ToString();                // 1 → "1"
            var chapterChunks = await baseQuery
                .Where(c => EF.Functions.Like(c.DocumentName, $"%Chapter {padded}%")
                         || EF.Functions.Like(c.DocumentName, $"%Chapter {plain}%")
                         || EF.Functions.Like(c.DocumentName, $"%Chuong {padded}%")
                         || EF.Functions.Like(c.DocumentName, $"%Chuong {plain}%"))
                .OrderBy(c => c.ChunkIndex)          // lấy từ đầu tài liệu để tóm tắt
                .Take(limit)
                .ToListAsync();

            if (chapterChunks.Count > 0) return chapterChunks;
        }

        // 2) Tìm theo từng từ khóa (≥3 ký tự, bỏ từ thừa) trong cả Nội dung lẫn Tên tài liệu.
        var keywords = query
            .Split(new[] { ' ', ',', '.', '?', '!', ':', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim())
            .Where(w => w.Length >= 3 && !StopWords.Contains(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var candidates = new List<DocumentChunk>();
        foreach (var keyword in keywords)
        {
            var kw = keyword;
            var matches = await baseQuery
                .Where(c => EF.Functions.Like(c.Content, $"%{kw}%")
                         || EF.Functions.Like(c.DocumentName, $"%{kw}%"))
                .Take(limit * 3)
                .ToListAsync();
            candidates.AddRange(matches);
        }

        if (candidates.Count > 0)
        {
            // Xếp hạng theo số từ khóa khớp (chunk khớp nhiều từ → ưu tiên hơn)
            return candidates
                .GroupBy(c => c.Id)
                .OrderByDescending(g => g.Count())
                .Take(limit)
                .Select(g => g.First())
                .ToList();
        }

        // 3) Fallback: không khớp từ khóa nào nhưng chat đang gắn với một môn →
        //    trả về các đoạn đầu của tài liệu trong môn để câu hỏi dạng "tóm tắt"
        //    vẫn có ngữ cảnh cho LLM xử lý (thay vì báo "không tìm thấy").
        if (!string.IsNullOrEmpty(subjectId))
        {
            return await baseQuery
                .OrderBy(c => c.DocumentName)
                .ThenBy(c => c.ChunkIndex)
                .Take(limit)
                .ToListAsync();
        }

        return new List<DocumentChunk>();
    }

    // Tìm số chương từ câu hỏi: "chuong 1", "chương 01", "chapter 2", "bai 3", "buoi 4"...
    private static int? ExtractChapterNumber(string query)
    {
        var m = Regex.Match(query,
            @"(?:chuong|chương|chapter|chap|bai|bài|buoi|buổi)\s*0*(\d{1,2})",
            RegexOptions.IgnoreCase);
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : null;
    }

    public async Task<List<DocumentChunk>> GetByDocumentAsync(string documentId)
        => await _context.DocumentChunks
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync();

    public async Task DeleteByDocumentAsync(string documentId)
    {
        var chunks = await _context.DocumentChunks
            .Where(c => c.DocumentId == documentId).ToListAsync();
        _context.DocumentChunks.RemoveRange(chunks);
        await _context.SaveChangesAsync();
    }

    public async Task<long> CountAsync() => await _context.DocumentChunks.LongCountAsync();
}
