namespace ServiceLayer.Dtos;

public class RblSuiteResult
{
    public bool Success { get; set; }
    public string SubjectCode { get; set; } = "";
    public string? SubjectId { get; set; }
    public long TotalMs { get; set; }
    public List<string> Completed { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
