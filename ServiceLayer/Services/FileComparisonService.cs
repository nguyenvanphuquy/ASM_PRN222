using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceLayer.Dtos;
using ServiceLayer.Settings;

namespace ServiceLayer.Services;

public class FileComparisonService : IFileComparisonService
{
    private readonly IDocumentService _documentService;
    private readonly ITextExtractor _extractor;
    private readonly IDocumentFileStore _fileStore;
    private readonly HttpClient _http;
    private readonly GroqSettings _groq;
    private readonly ILogger<FileComparisonService> _logger;

    public FileComparisonService(
        IDocumentService documentService,
        ITextExtractor extractor,
        IDocumentFileStore fileStore,
        HttpClient http,
        IOptions<GroqSettings> groqOptions,
        ILogger<FileComparisonService> logger)
    {
        _documentService = documentService;
        _extractor = extractor;
        _fileStore = fileStore;
        _http = http;
        _groq = groqOptions.Value;
        _logger = logger;
    }

    public async Task<FileComparisonResult> CompareAsync(
        string documentId1,
        string documentId2,
        CancellationToken ct = default)
    {
        // Mở file 1
        var file1 = await _documentService.OpenAsync(documentId1)
            ?? throw new InvalidOperationException($"Không tìm thấy file với ID: {documentId1}");
        // Mở file 2
        var file2 = await _documentService.OpenAsync(documentId2)
            ?? throw new InvalidOperationException($"Không tìm thấy file với ID: {documentId2}");

        using (file1.Content)
        using (file2.Content)
        {
            return await CompareStreamsAsync(
                file1.Content, file1.FileName, file1.ContentType,
                file2.Content, file2.FileName, file2.ContentType,
                ct);
        }
    }

    public async Task<FileComparisonResult> CompareStreamsAsync(
        Stream stream1, string fileName1, string contentType1,
        Stream stream2, string fileName2, string contentType2,
        CancellationToken ct = default)
    {
        // Extract text từ 2 file
        var pages1 = _extractor.Extract(stream1, fileName1, contentType1);
        var pages2 = _extractor.Extract(stream2, fileName2, contentType2);

        var text1 = BuildFullText(pages1, fileName1);
        var text2 = BuildFullText(pages2, fileName2);

        if (string.IsNullOrWhiteSpace(_groq.ApiKey))
        {
            return BuildFallbackResult(fileName1, fileName2, text1, text2);
        }

        return await CallGroqForComparisonAsync(fileName1, text1, fileName2, text2, ct);
    }

    // ──────────────────────────────────────────
    //  PRIVATE HELPERS
    // ──────────────────────────────────────────

    private static string BuildFullText(List<(int Page, string Text)> pages, string fileName)
    {
        if (pages.Count == 0) return $"[{fileName}: Không trích xuất được nội dung]";

        var sb = new StringBuilder();
        foreach (var (page, text) in pages)
        {
            sb.AppendLine($"--- Trang {page} ---");
            sb.AppendLine(text);
            sb.AppendLine();
        }
        // Giới hạn 6000 ký tự mỗi file để không vượt context limit
        var full = sb.ToString();
        return full.Length > 6000 ? full.Substring(0, 6000) + "\n...[bị cắt bớt]" : full;
    }

    private async Task<FileComparisonResult> CallGroqForComparisonAsync(
        string fileName1, string text1,
        string fileName2, string text2,
        CancellationToken ct)
    {
        var systemPrompt = BuildComparisonSystemPrompt();
        var userPrompt = BuildComparisonUserPrompt(fileName1, text1, fileName2, text2);

        var payload = new
        {
            model = _groq.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userPrompt }
            },
            temperature = 0.1, // rất thấp để kết quả nhất quán
            max_tokens = 2048
        };

        var url = $"{_groq.BaseUrl}/chat/completions";
        var body = JsonSerializer.Serialize(payload);

        try
        {
            int[] retryDelaysMs = [1500, 3000, 6000];
            HttpResponseMessage res = null!;
            string responseText = string.Empty;

            for (int attempt = 0; attempt <= retryDelaysMs.Length; attempt++)
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                req.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _groq.ApiKey);

                res = await _http.SendAsync(req, ct);
                responseText = await res.Content.ReadAsStringAsync(ct);

                if (res.IsSuccessStatusCode || (int)res.StatusCode != 429) break;

                if (attempt < retryDelaysMs.Length)
                {
                    _logger.LogWarning("Groq 429 (compare) – retry {A}/{M}", attempt + 1, retryDelaysMs.Length);
                    await Task.Delay(retryDelaysMs[attempt], ct);
                }
            }

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("Groq API error {Status} during file comparison", res.StatusCode);
                return BuildFallbackResult(fileName1, fileName2, text1, text2,
                    $"API lỗi {(int)res.StatusCode} — không thể phân tích bằng AI.");
            }

            using var doc = JsonDocument.Parse(responseText);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            return ParseGroqResponse(fileName1, fileName2, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Groq file comparison call failed");
            return BuildFallbackResult(fileName1, fileName2, text1, text2,
                "Lỗi mạng khi gọi AI — vui lòng thử lại.");
        }
    }

    private static string BuildComparisonSystemPrompt() => """
        Bạn là chuyên gia kiểm tra và so sánh tài liệu. Nhiệm vụ của bạn là phân tích 2 tài liệu được cung cấp,
        tìm ra TẤT CẢ các điểm khác biệt, lỗi sai, mâu thuẫn, nội dung thiếu/thừa giữa chúng.

        Trả lời CHÍNH XÁC theo JSON format sau (không thêm text ngoài JSON):
        {
          "summary": "Tóm tắt tổng quan về sự khác biệt giữa 2 file (2-4 câu)",
          "total": <số nguyên — tổng số điểm khác biệt>,
          "differences": [
            {
              "number": 1,
              "location": "Trang X, đoạn Y / Phần Z",
              "description": "Mô tả chi tiết điểm khác biệt",
              "type": "ContentError|Typo|MissingContent|ExtraContent|FormatDifference|Other",
              "file1_excerpt": "Đoạn trích nguyên văn từ File 1 (nếu có, tối đa 150 ký tự)",
              "file2_excerpt": "Đoạn trích nguyên văn từ File 2 (nếu có, tối đa 150 ký tự)"
            }
          ]
        }

        Quy tắc phân loại:
        - ContentError: Số liệu sai, tên riêng sai, sự kiện sai, thông tin mâu thuẫn
        - Typo: Lỗi chính tả, đánh máy sai
        - MissingContent: Có ở File 1 nhưng không có ở File 2
        - ExtraContent: Có ở File 2 nhưng không có ở File 1
        - FormatDifference: Cấu trúc, định dạng, thứ tự khác nhau
        - Other: Loại khác

        Nếu 2 file giống hệt nhau: total = 0, differences = [], summary = "Hai file có nội dung giống nhau."
        """;

    private static string BuildComparisonUserPrompt(
        string name1, string text1,
        string name2, string text2) =>
        $"""
        Hãy so sánh 2 tài liệu sau đây:

        ═══════════════════════════════
        FILE 1: {name1}
        ═══════════════════════════════
        {text1}

        ═══════════════════════════════
        FILE 2: {name2}
        ═══════════════════════════════
        {text2}

        Trả về JSON theo đúng format đã quy định.
        """;

    private static FileComparisonResult ParseGroqResponse(
        string fileName1, string fileName2, string rawContent)
    {
        // Trích JSON từ response (AI đôi khi thêm markdown code block)
        var jsonStr = rawContent.Trim();
        if (jsonStr.StartsWith("```"))
        {
            var start = jsonStr.IndexOf('{');
            var end = jsonStr.LastIndexOf('}');
            if (start >= 0 && end > start)
                jsonStr = jsonStr.Substring(start, end - start + 1);
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonStr);
            var root = doc.RootElement;

            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "";
            var total = root.TryGetProperty("total", out var t) ? t.GetInt32() : 0;

            var diffs = new List<ComparisonDifference>();
            if (root.TryGetProperty("differences", out var diffArr) && diffArr.ValueKind == JsonValueKind.Array)
            {
                int idx = 1;
                foreach (var item in diffArr.EnumerateArray())
                {
                    var typeStr = item.TryGetProperty("type", out var tp) ? tp.GetString() ?? "Other" : "Other";
                    var diffType = Enum.TryParse<DiffType>(typeStr, out var dt) ? dt : DiffType.Other;

                    diffs.Add(new ComparisonDifference(
                        Number: item.TryGetProperty("number", out var n) ? n.GetInt32() : idx,
                        Location: item.TryGetProperty("location", out var l) ? l.GetString() ?? "" : "",
                        Description: item.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                        Type: diffType,
                        File1Excerpt: item.TryGetProperty("file1_excerpt", out var e1) ? e1.GetString() : null,
                        File2Excerpt: item.TryGetProperty("file2_excerpt", out var e2) ? e2.GetString() : null
                    ));
                    idx++;
                }
            }

            return new FileComparisonResult(
                DocumentId1: "",
                FileName1: fileName1,
                DocumentId2: "",
                FileName2: fileName2,
                Summary: summary,
                TotalDifferences: total,
                Differences: diffs
            );
        }
        catch (JsonException)
        {
            // Nếu không parse được JSON, trả về raw text trong Summary
            return new FileComparisonResult(
                DocumentId1: "",
                FileName1: fileName1,
                DocumentId2: "",
                FileName2: fileName2,
                Summary: "AI đã phân tích nhưng kết quả không parse được JSON. Chi tiết: " + rawContent.Substring(0, Math.Min(500, rawContent.Length)),
                TotalDifferences: 0,
                Differences: []
            );
        }
    }

    private static FileComparisonResult BuildFallbackResult(
        string fileName1, string fileName2,
        string text1, string text2,
        string errorNote = "")
    {
        // Fallback đơn giản: so sánh độ dài
        var lenDiff = Math.Abs(text1.Length - text2.Length);
        var note = string.IsNullOrEmpty(errorNote) ? "" : $" ({errorNote})";

        return new FileComparisonResult(
            DocumentId1: "",
            FileName1: fileName1,
            DocumentId2: "",
            FileName2: fileName2,
            Summary: $"Không có API key Groq để phân tích AI{note}. " +
                     $"File 1 có {text1.Length} ký tự, File 2 có {text2.Length} ký tự " +
                     $"(chênh lệch {lenDiff} ký tự). Vui lòng cấu hình Groq API key để sử dụng tính năng này.",
            TotalDifferences: 0,
            Differences: []
        );
    }
}
