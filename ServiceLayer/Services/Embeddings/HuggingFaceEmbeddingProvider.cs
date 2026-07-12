using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ServiceLayer.Services.Implementations;

namespace ServiceLayer.Services.Embeddings;

/// <summary>
/// Embedding qua HuggingFace Inference (router hf-inference, pipeline feature-extraction).
/// Hỗ trợ cả model sentence-transformers (trả về 1 vector đã pool) lẫn model thô như PhoBERT
/// (trả về embedding theo từng token → tự mean-pooling). Cần HuggingFace token (miễn phí) đặt
/// trong Cài đặt RBL (Rbl.HuggingFaceApiToken).
/// </summary>
public class HuggingFaceEmbeddingProvider : IEmbeddingProvider
{
    public string Name => "HuggingFace";
    private readonly HttpClient _http;
    private readonly ISystemSettingService _settingService;

    public HuggingFaceEmbeddingProvider(HttpClient http, ISystemSettingService settingService)
    {
        _http = http;
        _settingService = settingService;
    }

    public async Task<float[]> GetEmbeddingAsync(string text, string model)
    {
        var apiToken = await _settingService.GetSettingAsync("Rbl.HuggingFaceApiToken", "");
        if (string.IsNullOrEmpty(apiToken))
            throw new InvalidOperationException("Thiếu HuggingFace API Token (đặt trong Cài đặt RBL).");

        string modelId = model switch
        {
            "PhoBERT-base" => "vinai/phobert-base",
            "bge-m3" => "BAAI/bge-m3",
            _ => "intfloat/multilingual-e5-base" // default (multilingual-e5-base)
        };

        // Endpoint hiện hành của HuggingFace (api-inference cũ đã ngừng).
        var url = $"https://router.huggingface.co/hf-inference/models/{modelId}/pipeline/feature-extraction";
        var payload = JsonSerializer.Serialize(new
        {
            inputs = text,
            // Chờ model cold-start thay vì trả 503, và cắt bớt input quá dài.
            options = new { wait_for_model = true },
            truncate = true
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        var response = await _http.SendAsync(req);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"HF {(int)response.StatusCode}: {Trim(json)}");

        return ParseEmbedding(json);
    }

    /// <summary>
    /// feature-extraction có thể trả về nhiều dạng: [f,...] (đã pool), [[f,...],...] (theo token,
    /// cần mean-pool), hoặc [[[f,...]]] (batch × token × hidden). Chuẩn hoá về 1 vector.
    /// </summary>
    private static float[] ParseEmbedding(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var err))
            throw new InvalidOperationException($"HF error: {err.GetString()}");
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            throw new InvalidOperationException("HF trả về dữ liệu embedding rỗng/không hợp lệ.");

        var first = root[0];

        // Dạng 1: [f, f, ...] → đã là vector câu.
        if (first.ValueKind == JsonValueKind.Number)
            return root.EnumerateArray().Select(e => e.GetSingle()).ToArray();

        // Dạng 3: [[[...]]] → bóc lớp batch ngoài cùng.
        if (first.ValueKind == JsonValueKind.Array && first.GetArrayLength() > 0
            && first[0].ValueKind == JsonValueKind.Array)
            return MeanPool(first);

        // Dạng 2: [[...token...]] → mean-pool theo token.
        return MeanPool(root);
    }

    private static float[] MeanPool(JsonElement tokens)
    {
        int rows = tokens.GetArrayLength();
        if (rows == 0) throw new InvalidOperationException("HF trả về 0 token.");
        int dim = tokens[0].GetArrayLength();
        var sum = new float[dim];
        foreach (var tok in tokens.EnumerateArray())
        {
            int i = 0;
            foreach (var v in tok.EnumerateArray())
                if (i < dim) sum[i++] += v.GetSingle();
        }
        for (int i = 0; i < dim; i++) sum[i] /= rows;
        return sum;
    }

    private static string Trim(string s) => s.Length > 200 ? s[..200] : s;
}
