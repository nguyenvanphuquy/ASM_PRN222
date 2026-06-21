using DataAccessLayer.Entities;

namespace ServiceLayer.Services;

public enum UploadOutcome
{
    Created,    // new document indexed
    Replaced,   // same filename existed with different content — old one replaced
    Duplicate   // identical file already indexed in this subject — skipped
}

public record UploadResult(ServiceLayer.DTOs.DocumentDto Document, UploadOutcome Outcome);

/// <summary>An openable original file: the raw stream plus the metadata needed to serve it.</summary>
public record DocumentFile(Stream Content, string ContentType, string FileName);

/// <summary>A page/slide of extracted text, used to preview office files that browsers can't render.</summary>
public record DocumentTextPage(int Page, string Text);

public interface IDocumentService
{
    Task<UploadResult> UploadAsync(Stream content, string fileName, string contentType, long fileSize, string subjectId, string uploadedByUserId, string? title = null, string? chapterId = null);
    Task<List<ServiceLayer.DTOs.DocumentDto>> GetBySubjectAsync(string subjectId);
    Task<List<ServiceLayer.DTOs.DocumentDto>> GetByChapterAsync(string chapterId);
    Task<List<ServiceLayer.DTOs.DocumentDto>> GetAllAsync();
    Task<List<ServiceLayer.DTOs.DocumentDto>> SearchAsync(string? subjectId, string? query);
    Task<ServiceLayer.DTOs.DocumentDto?> GetByIdAsync(string documentId);
    Task<bool> HasFileAsync(string documentId);
    // The indexed chunks of a document — shown in the web viewer so users see how the file was split for the AI.
    Task<List<ServiceLayer.DTOs.DocumentChunkDto>> GetChunksAsync(string documentId);
    // Re-runs chunking on an already-uploaded document, replacing its chunks. Returns the new
    // chunk count, or null if the document/original file is missing.
    Task<int?> ReChunkAsync(string documentId);
    Task<DocumentFile?> OpenAsync(string documentId);
    Task<List<DocumentTextPage>?> ExtractTextAsync(string documentId);
    // Converts a .docx to formatted HTML (headings, lists, tables, embedded images) for in-web viewing.
    Task<string?> ExtractDocxHtmlAsync(string documentId);
    Task DeleteAsync(string documentId);
    
    Task<bool> ApproveAsync(string documentId);
    Task<bool> RejectAsync(string documentId);
}



