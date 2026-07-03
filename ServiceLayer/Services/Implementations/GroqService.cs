using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DataAccessLayer.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;
using ServiceLayer.Settings;

namespace ServiceLayer.Services.Implementations;

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

    public Task<LlmResult> GenerateAnswerAsync(
        string question,
        IReadOnlyList<DocumentChunk> contextChunks,
        IReadOnlyList<ChatMessage> history,
        CancellationToken ct = default)
        => GenerateAnswerWithModelAsync(_groq.Model, question, contextChunks, history, ct);

    public async Task<LlmResult> GenerateAnswerWithModelAsync(
        string model,
        string question,
        IReadOnlyList<DocumentChunk> contextChunks,
        IReadOnlyList<ChatMessage> history,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_groq.ApiKey))
            return new LlmResult(BuildFallback(contextChunks), model, 0, 0, 0, 0, IsError: true);

        var systemPrompt = BuildSystemPrompt(contextChunks);
        var messages = new List<object> { new { role = "system", content = systemPrompt } };

        // Add recent history (last 6 turns) as conversation context
        foreach (var msg in history.TakeLast(6))
            messages.Add(new { role = msg.Role == "assistant" ? "assistant" : "user", content = msg.Content });

        messages.Add(new { role = "user", content = question });

        var result = await CallAsync(model, messages, 1024, ct);
        if (result.IsError)
        {
            var fb = BuildFallback(contextChunks) + $"\n\n_(Không gọi được model {model}, đã dùng fallback.)_";
            return result with { Content = fb };
        }
        return result;
    }

    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_groq.ApiKey))
            return "Chưa cấu hình API Key cho Groq.";

        var messages = new List<object> { new { role = "user", content = prompt } };
        var result = await CallAsync(_groq.Model, messages, 2048, ct);
        return result.IsError ? "Lỗi khi gọi API phân tích." : result.Content;
    }

    /// <summary>
    /// Gọi Groq chat/completions, trả về nội dung + số token (usage) + độ trễ.
    /// Tự động retry khi bị 429 (rate limit).
    /// </summary>
    private async Task<LlmResult> CallAsync(string model, List<object> messages, int maxTokens, CancellationToken ct)
    {
        var payload = new { model, messages, temperature = 0.3, max_tokens = maxTokens };
        var url = $"{_groq.BaseUrl}/chat/completions";
        var body = JsonSerializer.Serialize(payload);
        var sw = Stopwatch.StartNew();

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
                    _logger.LogWarning("Groq 429 ({Model}) – retry {Attempt}/{Max} after {Delay}ms", model, attempt + 1, retryDelaysMs.Length, retryDelaysMs[attempt]);
                    await Task.Delay(retryDelaysMs[attempt], ct);
                }
            }

            sw.Stop();

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("Groq API error {Status} ({Model}): {Body}", res.StatusCode, model, text);
                return new LlmResult(string.Empty, model, 0, 0, 0, sw.ElapsedMilliseconds, IsError: true);
            }

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

            int prompt = 0, completion = 0, total = 0;
            if (root.TryGetProperty("usage", out var usage))
            {
                prompt = usage.TryGetProperty("prompt_tokens", out var p) ? p.GetInt32() : 0;
                completion = usage.TryGetProperty("completion_tokens", out var c) ? c.GetInt32() : 0;
                total = usage.TryGetProperty("total_tokens", out var t) ? t.GetInt32() : prompt + completion;
            }

            return new LlmResult(content.Trim(), model, prompt, completion, total, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Groq call failed ({Model})", model);
            return new LlmResult(string.Empty, model, 0, 0, 0, sw.ElapsedMilliseconds, IsError: true);
        }
    }

    private static string BuildSystemPrompt(IReadOnlyList<DocumentChunk> chunks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Bạn là trợ lý học tập AI cho sinh viên Việt Nam, trả lời bằng tiếng Việt.");
        sb.AppendLine("Quy tắc bắt buộc:");
        sb.AppendLine("1. CHỈ trả lời dựa trên nội dung tài liệu được cung cấp bên dưới.");
        sb.AppendLine("2. Nếu tài liệu không đủ thông tin để trả lời, hãy trả lời chính xác câu sau và không nói gì thêm: \"Tôi không tìm thấy thông tin này trong tài liệu môn học.\"");
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
            return "Tôi không tìm thấy thông tin này trong tài liệu môn học.";

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
}
