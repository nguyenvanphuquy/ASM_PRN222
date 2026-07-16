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

public class CerebrasService : ICerebrasService
{
    private readonly HttpClient _http;
    private readonly CerebrasSettings _cerebras;
    private readonly ILogger<CerebrasService> _logger;

    public CerebrasService(HttpClient http, IOptions<CerebrasSettings> cerebrasOptions, ILogger<CerebrasService> logger)
    {
        _http = http;
        _cerebras = cerebrasOptions.Value;
        _logger = logger;
    }

    public Task<LlmResult> GenerateAnswerAsync(
        string question,
        IReadOnlyList<DocumentChunk> contextChunks,
        IReadOnlyList<ChatMessage> history,
        CancellationToken ct = default)
        => GenerateAnswerWithModelAsync(_cerebras.Model, question, contextChunks, history, ct);

    public async Task<LlmResult> GenerateAnswerWithModelAsync(
        string model,
        string question,
        IReadOnlyList<DocumentChunk> contextChunks,
        IReadOnlyList<ChatMessage> history,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_cerebras.ApiKey))
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
        if (string.IsNullOrWhiteSpace(_cerebras.ApiKey))
            return "Chưa cấu hình API Key cho Cerebras.";

        var messages = new List<object> { new { role = "user", content = prompt } };
        var result = await CallAsync(_cerebras.Model, messages, 2048, ct);
        return result.IsError ? "Lỗi khi gọi API phân tích." : result.Content;
    }

    public async Task<LlmResult> GenerateParametricAnswerAsync(
        string question,
        string? subjectName = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_cerebras.ApiKey))
            return new LlmResult(
                "Chưa cấu hình Cerebras API Key — không thể gọi nhánh Fine-tuned.",
                _cerebras.Model, 0, 0, 0, 0, IsError: true);

        var systemPrompt = BuildParametricSystemPrompt(subjectName);
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = question }
        };

        return await CallAsync(_cerebras.Model, messages, 1024, ct);
    }

    /// <summary>
    /// Prompt mô phỏng model đã fine-tune: trả lời từ parametric knowledge, không có chunk tài liệu.
    /// </summary>
    private static string BuildParametricSystemPrompt(string? subjectName)
    {
        var subject = string.IsNullOrWhiteSpace(subjectName) ? "học phần đại học" : subjectName;
        var sb = new StringBuilder();
        sb.AppendLine($"Bạn là chuyên gia môn «{subject}», đã được fine-tune trên kiến thức chuyên ngành.");
        sb.AppendLine("Bạn trả lời hoàn toàn từ kiến thức nội tại (parametric knowledge) — KHÔNG có tài liệu đính kèm trong prompt.");
        sb.AppendLine("Quy tắc:");
        sb.AppendLine("1. Trả lời bằng tiếng Việt, rõ ràng, có cấu trúc.");
        sb.AppendLine("2. Nếu không chắc chắn, nói rõ đây là suy luận từ kiến thức đã học (training), không phải trích dẫn tài liệu cụ thể.");
        sb.AppendLine("3. KHÔNG bịa số liệu/page/file name cụ thể như thể đang trích dẫn corpus.");
        sb.AppendLine("4. Không nhắc rằng bạn đang «mô phỏng» trừ khi người dùng hỏi về cơ chế.");
        return sb.ToString();
    }

    /// <summary>
    /// Gọi Cerebras chat/completions, trả về nội dung + số token (usage) + độ trễ.
    /// Tự động retry khi bị 429 (rate limit).
    /// </summary>
    private async Task<LlmResult> CallAsync(string model, List<object> messages, int maxTokens, CancellationToken ct)
    {
        var payload = new { model, messages, temperature = 0.1, max_tokens = maxTokens };
        var url = $"{_cerebras.BaseUrl}/chat/completions";
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
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _cerebras.ApiKey);

                res = await _http.SendAsync(req, ct);
                text = await res.Content.ReadAsStringAsync(ct);

                if (res.IsSuccessStatusCode || (int)res.StatusCode != 429)
                    break;

                if (attempt < retryDelaysMs.Length)
                {
                    _logger.LogWarning("Cerebras 429 ({Model}) – retry {Attempt}/{Max} after {Delay}ms", model, attempt + 1, retryDelaysMs.Length, retryDelaysMs[attempt]);
                    await Task.Delay(retryDelaysMs[attempt], ct);
                }
            }

            sw.Stop();

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("Cerebras API error {Status} ({Model}): {Body}", res.StatusCode, model, text);
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
            _logger.LogError(ex, "Cerebras call failed ({Model})", model);
            return new LlmResult(string.Empty, model, 0, 0, 0, sw.ElapsedMilliseconds, IsError: true);
        }
    }

    private static string BuildSystemPrompt(IReadOnlyList<DocumentChunk> chunks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an AI study assistant for college students. Always reply in English.");
        sb.AppendLine("Mandatory rules:");
        sb.AppendLine("1. Answer the question based ONLY on the provided document context below. Do not use external knowledge.");
        sb.AppendLine("2. If the context does not contain enough information to answer the question, reply with exactly: \"I cannot find this information in the course documents.\" and nothing else.");
        sb.AppendLine("3. You MUST include clear inline citations in your response (e.g., [1], [2], etc.) corresponding to the index of the context chunks that support your statements. Place the citation immediately after the sentence or information it supports.");
        sb.AppendLine("4. Keep the response friendly, concise, and structured using markdown.");
        sb.AppendLine();
        sb.AppendLine("=== DOCUMENT CONTEXT ===");
        if (chunks.Count == 0)
        {
            sb.AppendLine("(No context available — politely inform the user that there are no related documents.)");
        }
        else
        {
            int i = 1;
            foreach (var c in chunks)
            {
                sb.AppendLine($"[{i}] Source: {c.DocumentName} - Page {c.Page}");
                sb.AppendLine(c.Content);
                sb.AppendLine();
                i++;
            }
        }
        sb.AppendLine("=== END OF CONTEXT ===");
        return sb.ToString();
    }

    private static string BuildFallback(IReadOnlyList<DocumentChunk> chunks)
    {
        if (chunks.Count == 0)
            return "I cannot find this information in the course documents.";

        var sb = new StringBuilder();
        sb.AppendLine("Based on the course documents, I found the following relevant parts:");
        sb.AppendLine();
        int i = 1;
        foreach (var c in chunks)
        {
            var snippet = c.Content.Length > 400 ? c.Content.Substring(0, 400) + "..." : c.Content;
            sb.AppendLine($"[{i}] *{c.DocumentName} - Page {c.Page}*");
            sb.AppendLine(snippet);
            sb.AppendLine();
            i++;
        }
        return sb.ToString().Trim();
    }
}
