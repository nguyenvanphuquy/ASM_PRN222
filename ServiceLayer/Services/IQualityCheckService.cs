namespace ServiceLayer.Services;

public class QualityCheckResult
{
    public int Score { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Warnings { get; set; } = string.Empty;
}

public interface IQualityCheckService
{
    Task<QualityCheckResult> CheckQualityAsync(string extractedText);
}
