using System.Text.RegularExpressions;
using DataAccessLayer.Context;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

using System.Text.Json;

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
            return candidates
                .GroupBy(c => c.Id)
                .OrderByDescending(g => g.Count())
                .Take(limit)
                .Select(g => (g.First(), 0.5f + (g.Count() * 0.1f))) // Phỏng đoán điểm keyword
                .ToList();
        }

        if (!string.IsNullOrEmpty(subjectId))
        {
            var fallback = await baseQuery
                .OrderBy(c => c.DocumentName)
                .ThenBy(c => c.ChunkIndex)
                .Take(limit)
                .ToListAsync();
            return fallback.Select(c => (c, 0.1f)).ToList();
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


