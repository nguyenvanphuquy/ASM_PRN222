using System.Security.Cryptography;
using DataAccessLayer.Constants;
using DataAccessLayer.Entities;
using DataAccessLayer.Repositories;

namespace ServiceLayer.Services;

public class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _docRepo;
    private readonly IDocumentChunkRepository _chunkRepo;
    private readonly ITextExtractor _extractor;
    private readonly IChunker _chunker;
    private readonly IDocumentFileStore _fileStore;

    public DocumentService(IDocumentRepository docRepo, IDocumentChunkRepository chunkRepo,
        ITextExtractor extractor, IChunker chunker, IDocumentFileStore fileStore)
    {
        _docRepo = docRepo;
        _chunkRepo = chunkRepo;
        _extractor = extractor;
        _chunker = chunker;
        _fileStore = fileStore;
    }

    public async Task<UploadResult> UploadAsync(Stream content, string fileName, string contentType,
        long fileSize, string subjectId, string uploadedByUserId, string? title = null, string? chapterId = null)
    {
        // Buffer once so we can both hash the bytes and feed them to the extractor.
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms);
        var hash = Convert.ToHexString(SHA256.HashData(ms.ToArray())).ToLowerInvariant();

        // Exact same file (identical bytes) already indexed in this subject — skip re-indexing.
        var sameHash = await _docRepo.GetBySubjectAndHashAsync(subjectId, hash);
        if (sameHash != null)
            return new UploadResult(sameHash, UploadOutcome.Duplicate);

        // Same filename but different content — treat as an updated version: drop the old one first.
        var outcome = UploadOutcome.Created;
        var sameName = await _docRepo.GetBySubjectAndFileNameAsync(subjectId, fileName);
        if (sameName != null)
        {
            await DeleteAsync(sameName.Id);
            outcome = UploadOutcome.Replaced;
        }

        // Create the record first in "Processing" state so the document is always visible in the
        // list with its pipeline status — even if extraction/chunking fails afterwards.
        var doc = new Document
        {
            Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(fileName) : title.Trim(),
            FileName = fileName,
            ContentType = contentType,
            ContentHash = hash,
            FileSize = fileSize,
            SubjectId = subjectId,
            ChapterId = string.IsNullOrWhiteSpace(chapterId) ? null : chapterId,
            UploadedBy = uploadedByUserId,
            ChunkCount = 0,
            Status = DocumentStatuses.Processing
        };
        await _docRepo.CreateAsync(doc);

        // Persist the original file so it can be opened/downloaded from the web later.
        ms.Position = 0;
        await _fileStore.SaveAsync(doc.Id, fileName, ms);

        try
        {
            ms.Position = 0;
            var pages = _extractor.Extract(ms, fileName, contentType);
            var chunked = _chunker.Chunk(pages);

            if (chunked.Count > 0)
            {
                var chunks = chunked.Select((c, i) => new DocumentChunk
                {
                    DocumentId = doc.Id,
                    SubjectId = subjectId,
                    DocumentName = fileName,
                    ChunkIndex = i,
                    Content = c.Text,
                    Page = c.Page
                }).ToList();
                await _chunkRepo.InsertManyAsync(chunks);
            }

            doc.ChunkCount = chunked.Count;
            doc.Status = chunked.Count > 0 ? DocumentStatuses.Indexed : DocumentStatuses.Empty;
            await _docRepo.UpdateAsync(doc);
        }
        catch
        {
            // Mark the document Failed (visible in the list) and re-throw so the caller can surface the error.
            doc.Status = DocumentStatuses.Failed;
            await _docRepo.UpdateAsync(doc);
            throw;
        }

        return new UploadResult(doc, outcome);
    }

    public Task<List<Document>> GetBySubjectAsync(string subjectId) => _docRepo.GetBySubjectAsync(subjectId);
    public Task<List<Document>> GetByChapterAsync(string chapterId) => _docRepo.GetByChapterAsync(chapterId);
    public Task<List<Document>> GetAllAsync() => _docRepo.GetAllAsync();
    public Task<List<Document>> SearchAsync(string? subjectId, string? query) => _docRepo.SearchAsync(subjectId, query);
    public Task<Document?> GetByIdAsync(string documentId) => _docRepo.GetByIdAsync(documentId);

    public Task<List<DocumentChunk>> GetChunksAsync(string documentId) => _chunkRepo.GetByDocumentAsync(documentId);

    public async Task<int?> ReChunkAsync(string documentId)
    {
        var doc = await _docRepo.GetByIdAsync(documentId);
        if (doc == null) return null;

        // Re-chunking re-reads the original stored file and runs the chunker over it again.
        var stream = _fileStore.Open(doc.Id, doc.FileName);
        if (stream == null) return null; // uploaded before original-file storage existed — can't re-chunk

        try
        {
            List<(int Page, string Text)> pages;
            using (stream)
                pages = _extractor.Extract(stream, doc.FileName, doc.ContentType);

            var chunked = _chunker.Chunk(pages);

            // Replace the old chunks with the freshly produced ones.
            await _chunkRepo.DeleteByDocumentAsync(documentId);
            if (chunked.Count > 0)
            {
                var chunks = chunked.Select((c, i) => new DocumentChunk
                {
                    DocumentId = doc.Id,
                    SubjectId = doc.SubjectId,
                    DocumentName = doc.FileName,
                    ChunkIndex = i,
                    Content = c.Text,
                    Page = c.Page
                }).ToList();
                await _chunkRepo.InsertManyAsync(chunks);
            }

            doc.ChunkCount = chunked.Count;
            doc.Status = chunked.Count > 0 ? DocumentStatuses.Indexed : DocumentStatuses.Empty;
            await _docRepo.UpdateAsync(doc);

            return chunked.Count;
        }
        catch
        {
            doc.Status = DocumentStatuses.Failed;
            await _docRepo.UpdateAsync(doc);
            throw;
        }
    }

    public async Task<bool> HasFileAsync(string documentId)
    {
        var doc = await _docRepo.GetByIdAsync(documentId);
        return doc != null && _fileStore.Exists(doc.Id, doc.FileName);
    }

    public async Task<List<DocumentTextPage>?> ExtractTextAsync(string documentId)
    {
        var doc = await _docRepo.GetByIdAsync(documentId);
        if (doc == null) return null;

        var stream = _fileStore.Open(doc.Id, doc.FileName);
        if (stream == null) return null;

        using (stream)
        {
            var pages = _extractor.Extract(stream, doc.FileName, doc.ContentType);
            return pages.Select(p => new DocumentTextPage(p.Page, p.Text)).ToList();
        }
    }

    public async Task<DocumentFile?> OpenAsync(string documentId)
    {
        var doc = await _docRepo.GetByIdAsync(documentId);
        if (doc == null) return null;

        var stream = _fileStore.Open(doc.Id, doc.FileName);
        if (stream == null) return null; // file uploaded before original-file storage existed

        // Prefer a known type derived from the extension — the ContentType stored at upload time
        // is sometimes generic (e.g. application/octet-stream), which forces browsers to download.
        var contentType = ContentTypeFor(doc.FileName)
            ?? (string.IsNullOrWhiteSpace(doc.ContentType) ? "application/octet-stream" : doc.ContentType);
        return new DocumentFile(stream, contentType, doc.FileName);
    }

    private static string? ContentTypeFor(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".txt" => "text/plain; charset=utf-8",
        _ => null
    };

    public async Task<string?> ExtractDocxHtmlAsync(string documentId)
    {
        var doc = await _docRepo.GetByIdAsync(documentId);
        if (doc == null) return null;
        if (!Path.GetExtension(doc.FileName).Equals(".docx", StringComparison.OrdinalIgnoreCase)) return null;

        var stream = _fileStore.Open(doc.Id, doc.FileName);
        if (stream == null) return null;

        using (stream)
        {
            // Mammoth converts the .docx to semantic HTML and embeds images as data URIs.
            var converter = new Mammoth.DocumentConverter();
            var result = converter.ConvertToHtml(stream);
            return result.Value;
        }
    }

    public async Task DeleteAsync(string documentId)
    {
        var doc = await _docRepo.GetByIdAsync(documentId);
        await _chunkRepo.DeleteByDocumentAsync(documentId);
        await _docRepo.DeleteAsync(documentId);
        if (doc != null) _fileStore.Delete(doc.Id, doc.FileName);
    }
}
