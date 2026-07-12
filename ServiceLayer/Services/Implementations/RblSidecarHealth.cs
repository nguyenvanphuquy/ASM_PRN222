namespace ServiceLayer.Services.Implementations;

/// <summary>Kiểm tra sidecar embedding local (tools/embedding_server.py).</summary>
public static class RblSidecarHealth
{
    public static async Task<bool> IsOnlineAsync(HttpClient http, string baseUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return false;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/health");
            using var res = await http.SendAsync(req, ct);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
