using System.Security.Cryptography;
using DataAccessLayer.Constants;
using DataAccessLayer.Entities;
using DataAccessLayer.Repositories;
using ServiceLayer.Services.Embeddings;
using System.Text.Json;

namespace ServiceLayer.Services;

public class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _docRepo;
    private readonly IDocumentChunkRepository _chunkRepo;
    private readonly ITextExtractor _extractor;
    private readonly IDocumentFileStore _fileStore;
    private readonly IQualityCheckService _qualityCheckService;
    private readonly IChunkingService _chunkingService;
    private readonly INotificationService _notifier;
    private readonly AutoMapper.IMapper _mapper;

    public DocumentService(IDocumentRepository docRepo, IDocumentChunkRepository chunkRepo,
        ITextExtractor extractor, IDocumentFileStore fileStore,
        IQualityCheckService qualityCheckService, IChunkingService chunkingService,
        INotificationService notifier, AutoMapper.IMapper mapper)
    {
        _docRepo = docRepo;
        _chunkRepo = chunkRepo;
        _extractor = extractor;
        _fileStore = fileStore;
        _qualityCheckService = qualityCheckService;
        _chunkingService = chunkingService;
        _notifier = notifier;
        _mapper = mapper;
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
            return new UploadResult(_mapper.Map<ServiceLayer.DTOs.DocumentDto>(sameHash), UploadOutcome.Duplicate);

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
            Status = "Processing"
        };
        await _docRepo.CreateAsync(doc);

        ms.Position = 0;
        await _fileStore.SaveAsync(doc.Id, fileName, ms);

        try
        {
            ms.Position = 0;
            var pages = _extractor.Extract(ms, fileName, contentType);
            var extractedText = string.Join("\n\n", pages.Select(p => p.Text));

            doc.ExtractedText = extractedText;

            var qualityResult = await _qualityCheckService.CheckQualityAsync(extractedText);
            doc.QualityScore = qualityResult.Score;
            doc.QualitySummary = qualityResult.Summary;
            doc.QualityWarnings = qualityResult.Warnings;

            doc.Status = "Reviewing";
            await _docRepo.UpdateAsync(doc);

            // 🔔 Thông báo cho giảng viên: tài liệu đã qua kiểm tra AI, chờ duyệt
            await _notifier.SendAsync(uploadedByUserId, "info",
                "📎 Tài liệu đã sẵn sàng",
                $"\"{doc.Title}\" đã được AI đánh giá ({doc.QualityScore}/100). Vui lòng vào trang chi tiết để xem xet và duyệt.");
        }
        catch
        {
            doc.Status = DocumentStatuses.Failed;
            await _docRepo.UpdateAsync(doc);

            await _notifier.SendAsync(uploadedByUserId, "error",
                "❌ Upload thất bại",
                $"\"{doc.Title}\" gặp lỗi khi xử lý. Vui lòng thử lại.");
            throw;
        }

        return new UploadResult(_mapper.Map<ServiceLayer.DTOs.DocumentDto>(doc), outcome);
    }

    public async Task<List<ServiceLayer.DTOs.DocumentDto>> GetBySubjectAsync(string subjectId) { var entities = await _docRepo.GetBySubjectAsync(subjectId); return _mapper.Map<List<ServiceLayer.DTOs.DocumentDto>>(entities); }
    public async Task<List<ServiceLayer.DTOs.DocumentDto>> GetByChapterAsync(string chapterId) { var entities = await _docRepo.GetByChapterAsync(chapterId); return _mapper.Map<List<ServiceLayer.DTOs.DocumentDto>>(entities); }
    public async Task<List<ServiceLayer.DTOs.DocumentDto>> GetAllAsync() { var entities = await _docRepo.GetAllAsync(); return _mapper.Map<List<ServiceLayer.DTOs.DocumentDto>>(entities); }
    public async Task<List<ServiceLayer.DTOs.DocumentDto>> SearchAsync(string? subjectId, string? query) { var entities = await _docRepo.SearchAsync(subjectId, query); return _mapper.Map<List<ServiceLayer.DTOs.DocumentDto>>(entities); }
    public async Task<ServiceLayer.DTOs.DocumentDto?> GetByIdAsync(string documentId) { var entity = await _docRepo.GetByIdAsync(documentId); return _mapper.Map<ServiceLayer.DTOs.DocumentDto>(entity); }

    public async Task<List<ServiceLayer.DTOs.DocumentChunkDto>> GetChunksAsync(string documentId) { var entities = await _chunkRepo.GetByDocumentAsync(documentId); return _mapper.Map<List<ServiceLayer.DTOs.DocumentChunkDto>>(entities); }

    public async Task<int?> ReChunkAsync(string documentId)
    {
        var doc = await _docRepo.GetByIdAsync(documentId);
        if (doc == null) return null;

        if (string.IsNullOrWhiteSpace(doc.ExtractedText))
        {
            // Cố gắng trích xuất lại
            var stream = _fileStore.Open(doc.Id, doc.FileName);
            if (stream != null)
            {
                using (stream)
                {
                    var pages = _extractor.Extract(stream, doc.FileName, doc.ContentType);
                    doc.ExtractedText = string.Join("\n\n", pages.Select(p => p.Text));
                }
            }
        }

        try
        {
            int chunkCount = await _chunkingService.ChunkAndSaveAsync(doc.Id, doc.SubjectId, doc.FileName, doc.ExtractedText ?? "");
            
            doc.ChunkCount = chunkCount;
            doc.Status = chunkCount > 0 ? DocumentStatuses.Indexed : DocumentStatuses.Empty;
            await _docRepo.UpdateAsync(doc);

            return chunkCount;
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

    public async Task<bool> ApproveAsync(string documentId)
    {
        var doc = await _docRepo.GetByIdAsync(documentId);
        if (doc == null || doc.Status != "Reviewing") return false;

        doc.Status = "Indexing";
        await _docRepo.UpdateAsync(doc);

        try
        {
            int chunkCount = await _chunkingService.ChunkAndSaveAsync(doc.Id, doc.SubjectId, doc.FileName, doc.ExtractedText ?? "");
            doc.ChunkCount = chunkCount;
            doc.Status = chunkCount > 0 ? DocumentStatuses.Indexed : DocumentStatuses.Empty;
            await _docRepo.UpdateAsync(doc);

            // 🔔 Thông báo Indexed thành công
            await _notifier.SendAsync(doc.UploadedBy, "success",
                "✅ Tài liệu đã được duyệt",
                $"\"{doc.Title}\" đã được index {chunkCount} chunks và sẵn sàng cho chatbot.");

            return true;
        }
        catch
        {
            doc.Status = DocumentStatuses.Failed;
            await _docRepo.UpdateAsync(doc);

            await _notifier.SendAsync(doc.UploadedBy, "error",
                "❌ Index thất bại",
                $"\"{doc.Title}\" gặp lỗi khi tạo chunks. Vui lòng thử lại.");
            return false;
        }
    }

    public async Task<bool> RejectAsync(string documentId)
    {
        var doc = await _docRepo.GetByIdAsync(documentId);
        if (doc == null || doc.Status != "Reviewing") return false;

        doc.Status = "Rejected";
        await _docRepo.UpdateAsync(doc);

        // 🔔 Thông báo từ chối
        await _notifier.SendAsync(doc.UploadedBy, "warning",
            "🚫 Tài liệu bị từ chối",
            $"\"{doc.Title}\" đã bị từ chối bởi quản trị viên.");

        return true;
    }
}



