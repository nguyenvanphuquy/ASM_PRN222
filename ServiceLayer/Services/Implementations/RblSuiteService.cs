using System.Diagnostics;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;

namespace ServiceLayer.Services.Implementations;

/// <summary>Chạy nhanh bộ benchmark chuẩn cho module RBL (theo báo cáo thực nghiệm).</summary>
public class RblSuiteService : IRblSuiteService
{
    private readonly ISubjectService _subjects;
    private readonly IChunkingComparisonService _chunking;
    private readonly IEmbeddingComparisonService _embedding;
    private readonly IRagVsFineTunedComparisonService _ragVsFt;

    public RblSuiteService(
        ISubjectService subjects,
        IChunkingComparisonService chunking,
        IEmbeddingComparisonService embedding,
        IRagVsFineTunedComparisonService ragVsFt)
    {
        _subjects = subjects;
        _chunking = chunking;
        _embedding = embedding;
        _ragVsFt = ragVsFt;
    }

    public async Task<RblSuiteResult> RunStandardSuiteAsync(string userId, string subjectCode = "PRN222")
    {
        var sw = Stopwatch.StartNew();
        var result = new RblSuiteResult { SubjectCode = subjectCode };

        var subjects = await _subjects.GetAllAsync();
        var subject = subjects.FirstOrDefault(s =>
            string.Equals(s.Code, subjectCode, StringComparison.OrdinalIgnoreCase));
        if (subject == null)
        {
            result.Errors.Add($"Không tìm thấy môn {subjectCode}. Upload tài liệu trước khi chạy suite.");
            return result;
        }

        result.SubjectId = subject.Id;

        var steps = new (string Label, Func<Task> Run)[]
        {
            ("Chunking: LINQ trong C#",
                () => _chunking.CompareAsync("LINQ trong C# là gì và dùng để làm gì?", subject.Id, userId)),
            ("Embedding: Async/await",
                () => _embedding.CompareAsync("Async và await trong C# hoạt động thế nào?", subject.Id, userId)),
            ("RAG vs FT (trong tài liệu)",
                () => _ragVsFt.CompareAsync("LINQ trong C# là gì?", subject.Id, userId)),
            ("RAG vs FT (ngoài tài liệu)",
                () => _ragVsFt.CompareAsync("Delegate và event trong C# khác nhau thế nào?", subject.Id, userId)),
        };

        foreach (var (label, run) in steps)
        {
            try
            {
                await run();
                result.Completed.Add(label);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{label}: {ex.Message}");
            }
        }

        sw.Stop();
        result.TotalMs = sw.ElapsedMilliseconds;
        result.Success = result.Completed.Count > 0 && result.Errors.Count == 0;
        return result;
    }
}
