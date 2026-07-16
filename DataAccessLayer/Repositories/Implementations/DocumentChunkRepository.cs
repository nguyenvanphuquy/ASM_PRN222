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

    // Trần ứng viên cho MỖI từ khóa. Luôn phải đi kèm OrderBy: khóa chính là GUID ngẫu nhiên
    // nên Take() không kèm thứ tự sẽ trả về một tập tùy ý — chunk đúng có thể bị loại từ SQL
    // trước khi kịp chấm điểm. Đặt cao hơn hẳn limit để việc xếp hạng diễn ra trong bộ nhớ.
    private const int MaxCandidatesPerKeyword = 500;

    // Từ thừa tiếng Việt không dấu — bỏ đi để không làm nhiễu việc tìm theo từ khóa
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "tom", "tat", "cho", "toi", "tai", "lieu", "cua", "mon", "gium", "giup",
        "hay", "nhe", "voi", "nay", "minh", "ban", "oi", "duoc", "the", "nao",
        "lai", "di", "va", "cac", "mot", "nhung", "trong", "ve", "la", "co",
        "khong", "hoac", "thi", "se", "day", "dum", "list", "ke", "ra", "noi",
        "summarize", "summary", "about", "the", "please", "give", "show",
        "what", "when", "where", "which", "who", "whom", "whose", "why", "how",
        "is", "are", "was", "were", "do", "does", "did", "a", "an", "of", "to",
        // Từ chỉ Ý ĐỊNH hỏi định nghĩa ("define X", "định nghĩa X") — không phải nội dung.
        // Nếu để lọt, chúng bị đem đi khớp văn bản và làm loãng độ phủ của chunk đúng:
        // "define serialization" khi đó ưu ái chunk chỉ vì có chữ "define" trong ví dụ code.
        "define", "definition", "dinh", "nghia"
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

        // 2) Cosine Similarity (lấy candidate, chưa return ngay để còn trộn với keyword).
        var vectorCandidates = new List<(DocumentChunk Chunk, float Score)>();
        if (queryVector != null && queryVector.Length > 0)
        {
            var allChunks = await baseQuery.ToListAsync();

            vectorCandidates = allChunks.Select(c =>
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
            // Ngưỡng thấp hơn để không bỏ lỡ chunk đúng nhưng embedding lệch nhẹ.
            }).Where(x => x.Score > 0.08f)
              .OrderByDescending(x => x.Score)
              .Take(limit * 6)
              .ToList();
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
                .OrderBy(c => c.ChunkIndex)
                .Take(MaxCandidatesPerKeyword)
                .ToListAsync();
            candidates.AddRange(matches);
        }

        if (candidates.Count > 0)
        {
            int totalKw = keywords.Count;
            var keywordScored = candidates
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
                    // Với câu hỏi dài, chia cho toàn bộ keywords hay làm coverage quá thấp.
                    // Dùng mẫu số tối đa 3 keyword mạnh nhất để giữ chunk khớp một phần vẫn được ưu tiên.
                    int coverageDenom = Math.Max(1, Math.Min(totalKw, 3));
                    float coverage = totalKw == 0 ? 0f : (float)matched / coverageDenom;
                    coverage = Math.Clamp(coverage, 0f, 1f);
                    float tfBonus = 1f - (float)Math.Exp(-occurrences / 3.0);
                    float score = coverage * (0.85f + 0.15f * tfBonus);
                    return (Chunk: c, Score: Math.Clamp(score, 0.1f, 1.0f));
                })
                .ToList();

            // 4) Hybrid rerank: trộn vector + keyword để tránh lệch ngữ nghĩa khi có exact match mạnh.
            var merged = new Dictionary<string, (DocumentChunk Chunk, float Vector, float Keyword)>();

            foreach (var v in vectorCandidates)
                merged[v.Chunk.Id] = (v.Chunk, v.Score, 0f);

            foreach (var k in keywordScored)
            {
                if (merged.TryGetValue(k.Chunk.Id, out var old))
                    merged[k.Chunk.Id] = (old.Chunk, old.Vector, Math.Max(old.Keyword, k.Score));
                else
                    merged[k.Chunk.Id] = (k.Chunk, 0f, k.Score);
            }

            var q = query.Trim();
            var definitionTerm = ExtractDefinitionTerm(query);
            return merged.Values
                .Select(x =>
                {
                    var content = x.Chunk.Content ?? string.Empty;
                    float phraseBoost = (!string.IsNullOrWhiteSpace(q)
                        && content.Contains(q, StringComparison.OrdinalIgnoreCase))
                        ? 0.15f
                        : 0f;
                    float definitionBoost = definitionTerm != null ? DefinitionScore(content, definitionTerm) : 0f;

                    float finalScore = 0.65f * x.Vector + 0.90f * x.Keyword + phraseBoost + definitionBoost;
                    // Không clamp upper bound để định nghĩa rõ ràng vẫn đẩy chunk đúng lên top.
                    return (x.Chunk, Score: Math.Max(0.05f, finalScore));
                })
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Chunk.ChunkIndex)
                .Take(limit)
                .ToList();
        }

        // Không có keyword hit: dùng vector candidates nếu có.
        if (vectorCandidates.Count > 0)
            return vectorCandidates.Take(limit).ToList();

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

    /// <summary>
    /// Cụm thuật ngữ mà câu hỏi định nghĩa đang hỏi, hoặc null nếu không phải câu hỏi định nghĩa:
    /// "design pattern là gì" / "what is a design pattern" -> "design pattern".
    /// Phải giữ nguyên CỤM, không tách từng từ: chấm riêng "design" và "pattern" sẽ khiến một câu
    /// "The pattern is ..." bất kỳ vượt mặt đúng câu định nghĩa "A design pattern is ...".
    /// </summary>
    private static string? ExtractDefinitionTerm(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        var qNorm = RemoveDiacritics(query).ToLowerInvariant();
        var m = Regex.Match(qNorm, @"^\s*(.+?)\s+(?:nghia\s+la\s+gi|la\s+gi)\s*\?*\s*$");
        if (!m.Success)
            m = Regex.Match(qNorm, @"\b(?:what\s+is|what's|what\s+are|define|definition\s+of|dinh\s+nghia)\s+(.+?)\s*\?*\s*$");
        if (!m.Success) return null;
        // "a design pattern" -> "design pattern"
        var term = Regex.Replace(m.Groups[1].Value.Trim(), @"^(?:a|an|the)\s+", "");
        return term.Length == 0 ? null : term;
    }

    private const string DefinitionCopula = @"(?:is|are|refers\s+to|means|stands\s+for|la|duoc\s+goi\s+la)\b";

    // Đầu văn bản, dấu kết câu, hoặc một chuỗi khoảng trắng (có bộ chunker gộp dấu xuống dòng
    // sau tiêu đề thành khoảng trắng). Rồi bỏ qua ngoặc/nháy và mạo từ: "... context. A design
    // pattern is ...".
    private const string DefinitionBoundary = @"(?:^|[.!?:;\n]|\s{2,})[\s""“”'’(\[]*(?:(?:a|an|the)\s+)?";

    /// <summary>
    /// Mức độ "chunk này chính là câu định nghĩa của <paramref name="term"/>".
    /// Cần thiết vì với câu hỏi một từ khóa, mọi chunk chứa từ đó đều có coverage = 1 nên điểm
    /// keyword gần như bằng nhau — mục lục (nhắc lại thuật ngữ nhiều lần) do đó ngang điểm với
    /// câu định nghĩa thật, và tie-break theo ChunkIndex khiến mục lục luôn thắng.
    /// Tín hiệu phân biệt: "&lt;thuật ngữ&gt; is/là ..." đứng ĐẦU CÂU mới là câu định nghĩa
    /// ("Serialization is the act of ..."), còn giữa câu chỉ là nhắc tới
    /// ("In contrast, implicit serialization is initiated by .NET").
    /// So khớp trên bản bỏ dấu để "là" và "la" là một.
    /// </summary>
    private static float DefinitionScore(string content, string term)
    {
        if (string.IsNullOrEmpty(content)) return 0f;
        var text = RemoveDiacritics(content);
        // Thuật ngữ có thể được chú giải trước hệ từ — giáo trình tiếng Việt gần như luôn viết
        // kiểu này: "Tiến trình (Process) là một chương trình đang ...".
        var head = $@"{Regex.Escape(term)}(?:\s*\([^)]{{0,60}}\))?\s+{DefinitionCopula}";
        if (Regex.IsMatch(text, $@"{DefinitionBoundary}{head}", RegexOptions.IgnoreCase))
            return 0.40f;
        if (Regex.IsMatch(text, $@"\b{head}", RegexOptions.IgnoreCase))
            return 0.20f;
        return 0f;
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

    public async Task<DocumentChunk?> GetByDocumentAndIndexAsync(string documentId, int chunkIndex)
        => await _context.DocumentChunks
            .FirstOrDefaultAsync(c => c.DocumentId == documentId && c.ChunkIndex == chunkIndex);

    public async Task DeleteByDocumentAsync(string documentId)
    {
        var chunks = await _context.DocumentChunks
            .Where(c => c.DocumentId == documentId).ToListAsync();
        _context.DocumentChunks.RemoveRange(chunks);
        await _context.SaveChangesAsync();
    }

    public async Task<long> CountAsync() => await _context.DocumentChunks.LongCountAsync();
}


