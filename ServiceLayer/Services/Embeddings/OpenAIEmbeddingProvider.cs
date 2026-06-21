using System.Net.Http.Json;
using System.Text.Json;

namespace ServiceLayer.Services.Embeddings;

public class OpenAIEmbeddingProvider : IEmbeddingProvider
{
    public string Name => "OpenAI";
    private readonly HttpClient _http;
    private readonly ISystemSettingService _settingService;

    public OpenAIEmbeddingProvider(HttpClient http, ISystemSettingService settingService)
    {
        _http = http;
        _settingService = settingService;
    }

    public async Task<float[]> GetEmbeddingAsync(string text, string model)
    {
        var apiKey = await _settingService.GetSettingAsync("Rbl.OpenAIApiKey", "");
        if (string.IsNullOrEmpty(apiKey)) throw new InvalidOperationException("OpenAI API Key is missing.");

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        
        request.Content = JsonContent.Create(new
        {
            input = text,
            model = string.IsNullOrEmpty(model) ? "text-embedding-3-small" : model
        });

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var vector = doc.RootElement.GetProperty("data")[0].GetProperty("embedding").EnumerateArray().Select(e => e.GetSingle()).ToArray();
        return vector;
    }
}
