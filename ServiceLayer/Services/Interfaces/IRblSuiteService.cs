using ServiceLayer.Dtos;

namespace ServiceLayer.Services.Interfaces;

public interface IRblSuiteService
{
    /// <summary>Chạy bộ thực nghiệm chuẩn (chunking + embedding + 2× RAG vs FT) trên môn PRN222.</summary>
    Task<RblSuiteResult> RunStandardSuiteAsync(string userId, string subjectCode = "PRN222");
}
