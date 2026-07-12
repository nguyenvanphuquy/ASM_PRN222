using System.Text;
using System.Text.Json;
using ServiceLayer.Services.Implementations;

namespace ServiceLayer.Services.Embeddings;

/// <summary>
/// Gọi các embedding model MÃ NGUỒN MỞ trong đề (multilingual-e5-base, PhoBERT-base, bge-m3)
/// qua sidecar Python chạy local (sentence-transformers) — KHÔNG cần API key.
/// Sidecar: tools/embedding_server.py (mặc định http://127.0.0.1:8600).
/// URL có thể chỉnh qua Cài đặt RBL (Rbl.LocalEmbedUrl).
/// </summary>
public class LocalStEmbeddingProvider : IEmbeddingProvider
{
    public string Name => "LocalST";
    private const string DefaultUrl = "http://127.0.0.1:8600";
    private readonly HttpClient _http;
    private readonly ISystemSettingService _settingService;

    internal HttpClient Http => _http;

    public LocalStEmbeddingProvider(HttpClient http, ISystemSettingService settingService)
    {
        _http = http;
        _settingService = settingService;
    }

    public async Task<float[]> GetEmbeddingAsync(string text, string model)
    {
        var baseUrl = await _settingService.GetSettingAsync("Rbl.LocalEmbedUrl", DefaultUrl);
        if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = DefaultUrl;
        var url = $"{baseUrl.TrimEnd('/')}/embed";

        var payload = JsonSerializer.Serialize(new { model, text });
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        HttpResponseMessage res;
        string json;
        try
        {
            res = await _http.SendAsync(req);
            json = await res.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Chưa chạy embedding server local. Mở terminal và chạy: " +
                "python tools/embedding_server.py (cần: pip install flask sentence-transformers torch). " +
                $"Chi tiết: {ex.Message}");
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!res.IsSuccessStatusCode || root.TryGetProperty("error", out _))
        {
            var msg = root.TryGetProperty("error", out var e) ? e.GetString() : json;
            throw new InvalidOperationException($"Local embed lỗi ({(int)res.StatusCode}): {msg}");
        }

        return root.GetProperty("vector").EnumerateArray().Select(x => x.GetSingle()).ToArray();
    }

    /// <summary>Embed hàng loạt qua POST /embed/batch — nhanh hơn gọi tuần tự.</summary>
    public async Task<IReadOnlyList<float[]>> GetEmbeddingsBatchAsync(IReadOnlyList<string> texts, string model)
    {
        if (texts.Count == 0) return Array.Empty<float[]>();

        var baseUrl = await _settingService.GetSettingAsync("Rbl.LocalEmbedUrl", DefaultUrl);
        if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = DefaultUrl;
        var url = $"{baseUrl.TrimEnd('/')}/embed/batch";

        var payload = JsonSerializer.Serialize(new { model, texts });
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        HttpResponseMessage res;
        string json;
        try
        {
            res = await _http.SendAsync(req);
            json = await res.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Chưa chạy embedding server local. Chạy: python tools/embedding_server.py. " +
                $"Chi tiết: {ex.Message}");
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!res.IsSuccessStatusCode || root.TryGetProperty("error", out _))
        {
            var msg = root.TryGetProperty("error", out var e) ? e.GetString() : json;
            throw new InvalidOperationException($"Local embed batch lỗi ({(int)res.StatusCode}): {msg}");
        }

        return root.GetProperty("vectors").EnumerateArray()
            .Select(v => v.EnumerateArray().Select(x => x.GetSingle()).ToArray())
            .ToList();
    }
}
