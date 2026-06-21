using System.Net.Http.Json;
using System.Text.Json;

namespace ServiceLayer.Services.Embeddings;

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
        if (string.IsNullOrEmpty(apiToken)) throw new InvalidOperationException("HuggingFace API Token is missing.");

        string modelId = model switch
        {
            "PhoBERT-base" => "vinai/phobert-base",
            "bge-m3" => "BAAI/bge-m3",
            _ => "intfloat/multilingual-e5-base" // default
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"https://api-inference.huggingface.co/pipeline/feature-extraction/{modelId}");
        request.Headers.Add("Authorization", $"Bearer {apiToken}");
        request.Content = JsonContent.Create(new { inputs = new[] { text } });

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        
        // Inference API usually returns an array of arrays (one for each input)
        // [ [0.1, 0.2, ...] ]
        var vector = doc.RootElement[0].EnumerateArray().Select(e => e.GetSingle()).ToArray();
        return vector;
    }
}
