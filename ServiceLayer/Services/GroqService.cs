using System.Text;
using System.Text.Json;
using DataAccessLayer.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceLayer.Settings;

namespace ServiceLayer.Services;

public class GroqService : IGroqService
{
    private readonly HttpClient _http;
    private readonly GroqSettings _groq;
    private readonly ILogger<GroqService> _logger;

    public GroqService(HttpClient http, IOptions<GroqSettings> groqOptions, ILogger<GroqService> logger)
    {
        _http = http;
        _groq = groqOptions.Value;
        _logger = logger;
    }

    public async Task<string> GenerateAnswerAsync(
        string question,
        IReadOnlyList<DocumentChunk> contextChunks,
        IReadOnlyList<ChatMessage> history,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_groq.ApiKey))
            return BuildFallback(contextChunks);

        var systemPrompt = BuildSystemPrompt(contextChunks);
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        // Add recent history (last 6 turns) as conversation context
        foreach (var msg in history.TakeLast(6))
            messages.Add(new { role = msg.Role == "assistant" ? "assistant" : "user", content = msg.Content });

        messages.Add(new { role = "user", content = question });

        var payload = new
        {
            model = _groq.Model,
            messages,
            temperature = 0.3,
            max_tokens = 1024
        };

        var url = $"{_groq.BaseUrl}/chat/completions";
        var body = JsonSerializer.Serialize(payload);

        try
        {
            HttpResponseMessage res = null!;
            string text = string.Empty;
            int[] retryDelaysMs = [1500, 3000, 6000];

            for (int attempt = 0; attempt <= retryDelaysMs.Length; attempt++)
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _groq.ApiKey);

                res = await _http.SendAsync(req, ct);
                text = await res.Content.ReadAsStringAsync(ct);

                if (res.IsSuccessStatusCode || (int)res.StatusCode != 429)
                    break;

                if (attempt < retryDelaysMs.Length)
                {
                    _logger.LogWarning("Groq 429 – retry {Attempt}/{Max} after {Delay}ms", attempt + 1, retryDelaysMs.Length, retryDelaysMs[attempt]);
                    await Task.Delay(retryDelaysMs[attempt], ct);
                }
            }

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("Groq API error {Status}: {Body}", res.StatusCode, text);
                return BuildFallback(contextChunks) +
                       $"\n\n_(Groq API trả về {(int)res.StatusCode}, đã dùng fallback.)_";
            }

            using var doc = JsonDocument.Parse(text);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return content?.Trim() ?? BuildFallback(contextChunks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Groq call failed");
            return BuildFallback(contextChunks) + "\n\n_(Lỗi mạng khi gọi Groq, đã dùng fallback.)_";
        }
    }

    private static string BuildSystemPrompt(IReadOnlyList<DocumentChunk> chunks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Bạn là trợ lý học tập AI cho sinh viên Việt Nam, trả lời bằng tiếng Việt.");
        sb.AppendLine("Quy tắc bắt buộc:");
        sb.AppendLine("1. CHỈ trả lời dựa trên nội dung tài liệu được cung cấp bên dưới.");
        sb.AppendLine("2. Nếu tài liệu không đủ thông tin, nói rõ \"Mình chưa tìm thấy trong tài liệu\" — KHÔNG được bịa.");
        sb.AppendLine("3. KHÔNG tự ý chèn Nguồn (Source) hay Độ tin cậy vào câu trả lời vì giao diện đã tự động hiển thị chúng bên dưới.");
        sb.AppendLine("4. Giọng thân thiện, ngắn gọn, có thể dùng markdown.");
        sb.AppendLine();
        sb.AppendLine("=== NGỮ CẢNH TÀI LIỆU ===");
        if (chunks.Count == 0)
        {
            sb.AppendLine("(Không có ngữ cảnh — hãy lịch sự thông báo cho người dùng rằng chưa có tài liệu liên quan.)");
        }
        else
        {
            int i = 1;
            foreach (var c in chunks)
            {
                sb.AppendLine($"[{i}] Nguồn: {c.DocumentName} - Trang {c.Page}");
                sb.AppendLine(c.Content);
                sb.AppendLine();
                i++;
            }
        }
        sb.AppendLine("=== HẾT NGỮ CẢNH ===");
        return sb.ToString();
    }

    private static string BuildFallback(IReadOnlyList<DocumentChunk> chunks)
    {
        if (chunks.Count == 0)
            return "Mình chưa tìm thấy thông tin liên quan trong tài liệu môn học. Bạn thử upload thêm tài liệu hoặc đặt câu hỏi khác nhé.";

        var sb = new StringBuilder();
        sb.AppendLine("Dựa trên tài liệu môn học, mình tìm thấy các đoạn liên quan sau:");
        sb.AppendLine();
        int i = 1;
        foreach (var c in chunks)
        {
            var snippet = c.Content.Length > 400 ? c.Content.Substring(0, 400) + "..." : c.Content;
            sb.AppendLine($"[{i}] *{c.DocumentName} - Trang {c.Page}*");
            sb.AppendLine(snippet);
            sb.AppendLine();
            i++;
        }
        return sb.ToString().Trim();
    }

    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_groq.ApiKey))
            return "Chưa cấu hình API Key cho Groq.";

        var messages = new List<object>
        {
            new { role = "user", content = prompt }
        };

        var payload = new
        {
            model = _groq.Model,
            messages,
            temperature = 0.3,
            max_tokens = 2048
        };

        var url = $"{_groq.BaseUrl}/chat/completions";
        var body = JsonSerializer.Serialize(payload);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _groq.ApiKey);

            var res = await _http.SendAsync(req, ct);
            var text = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
                return $"Lỗi gọi API: {res.StatusCode}";

            using var doc = JsonDocument.Parse(text);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return content?.Trim() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Groq call failed in GenerateTextAsync");
            return "Lỗi khi gọi API phân tích.";
        }
    }
}


