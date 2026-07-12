using System.Text.RegularExpressions;
using DataAccessLayer.Context;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

using System.Text.Json;
using DataAccessLayer.Repositories.Interfaces;

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

    public async Task<List<(DocumentChunk Chunk, float Score)>> SearchAsync(string query, string? subjectId, int limit, float[]? queryVector = null)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<(DocumentChunk, float)>();

        var baseQuery = _context.DocumentChunks.AsQueryable();
        if (!string.IsNullOrEmpty(subjectId))
            baseQuery = baseQuery.Where(c => c.SubjectId == subjectId);

        // 1) Nếu người dùng nhắc tới "chương N"
        var chapterNo = ExtractChapterNumber(query);
        if (chapterNo.HasValue)
        {
            var n = chapterNo.Value;
            var padded = n.ToString("D2");
            var plain = n.ToString();
            var chapterChunks = await baseQuery
                .Where(c => EF.Functions.Like(c.DocumentName, $"%Chapter {padded}%")
                         || EF.Functions.Like(c.DocumentName, $"%Chapter {plain}%")
                         || EF.Functions.Like(c.DocumentName, $"%Chuong {padded}%")
                         || EF.Functions.Like(c.DocumentName, $"%Chuong {plain}%"))
                .OrderBy(c => c.ChunkIndex)
                .Take(limit)
                .ToListAsync();

            if (chapterChunks.Count > 0) return chapterChunks.Select(c => (c, 1.0f)).ToList();
        }

        // 2) Cosine Similarity
        if (queryVector != null && queryVector.Length > 0)
        {
            var allChunks = await baseQuery.ToListAsync();
            
            var scoredChunks = allChunks.Select(c =>
            {
                if (string.IsNullOrEmpty(c.VectorJson)) return (Chunk: c, Score: -1f);
                try
                {
                    var chunkVector = JsonSerializer.Deserialize<float[]>(c.VectorJson);
                    if (chunkVector == null || chunkVector.Length != queryVector.Length) return (Chunk: c, Score: -1f);
                    
                    float dotProduct = 0, normA = 0, normB = 0;
                    for (int i = 0; i < queryVector.Length; i++)
                    {
                        dotProduct += queryVector[i] * chunkVector[i];
                        normA += queryVector[i] * queryVector[i];
                        normB += chunkVector[i] * chunkVector[i];
                    }
                    float similarity = (normA == 0 || normB == 0) ? 0 : dotProduct / (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
                    return (Chunk: c, Score: similarity);
                }
                catch { return (Chunk: c, Score: -1f); }
            }).Where(x => x.Score > 0.3f)
              .OrderByDescending(x => x.Score)
              .Take(limit)
              .ToList();

            if (scoredChunks.Count > 0) return scoredChunks;
        }

        // 3) Keyword search
        var keywords = query
            .Split(new[] { ' ', ',', '.', '?', '!', ':', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim())
            // Bỏ dấu trước khi so với stopwords (stopwords là dạng không dấu),
            // nếu không các từ có dấu như "Tóm", "tắt", "tài", "liệu", "môn" sẽ lọt qua.
            .Where(w => w.Length >= 3 && !StopWords.Contains(RemoveDiacritics(w)))
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
            int totalKw = keywords.Count;
            return candidates
                .GroupBy(c => c.Id)
                .Select(g =>
                {
                    var c = g.First();
                    var content = c.Content ?? string.Empty;
                    var name = c.DocumentName ?? string.Empty;

                    int matched = 0;      // số từ khóa (distinct) khớp với chunk
                    int occurrences = 0;  // tổng số lần từ khóa xuất hiện trong nội dung
                    foreach (var kw in keywords)
                    {
                        int cnt = CountOccurrences(content, kw);
                        occurrences += cnt;
                        if (cnt > 0 || name.Contains(kw, StringComparison.OrdinalIgnoreCase))
                            matched++;
                    }

                    // Độ phủ từ khóa là tín hiệu chính (khớp đủ từ khóa -> điểm cao),
                    // cộng thêm thưởng theo tần suất xuất hiện (bão hòa dần).
                    float coverage = totalKw == 0 ? 0f : (float)matched / totalKw;
                    float tfBonus = 1f - (float)Math.Exp(-occurrences / 3.0);
                    float score = coverage * (0.85f + 0.15f * tfBonus);
                    return (Chunk: c, Score: Math.Clamp(score, 0.1f, 1.0f));
                })
                .OrderByDescending(x => x.Score)
                .Take(limit)
                .ToList();
        }

        return new List<(DocumentChunk, float)>();
    }

    // Tìm số chương từ câu hỏi: "chuong 1", "chương 01", "chapter 2", "bai 3", "buoi 4"...
    private static int? ExtractChapterNumber(string query)
    {
        var m = Regex.Match(query,
            @"(?:chuong|chương|chapter|chap|bai|bài|buoi|buổi)\s*0*(\d{1,2})",
            RegexOptions.IgnoreCase);
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : null;
    }

    // Bỏ dấu tiếng Việt: "Tóm tắt liệu" -> "Tom tat lieu", "đường" -> "duong".
    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString()
            .Normalize(System.Text.NormalizationForm.FormC)
            .Replace('đ', 'd').Replace('Đ', 'D');
    }

    // Đếm số lần 'term' xuất hiện trong 'text' (không phân biệt hoa thường).
    private static int CountOccurrences(string text, string term)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(term)) return 0;
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(term, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            idx += term.Length;
        }
        return count;
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


