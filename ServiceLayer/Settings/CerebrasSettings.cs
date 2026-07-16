namespace ServiceLayer.Settings;

public class CerebrasSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-oss-120b";
    public string BaseUrl { get; set; } = "https://api.cerebras.ai/v1";
}
