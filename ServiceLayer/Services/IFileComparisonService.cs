using ServiceLayer.Dtos;

namespace ServiceLayer.Services;

public interface IFileComparisonService
{
    /// <summary>
    /// So sánh 2 documents đã upload và dùng AI để phát hiện lỗi sai, khác biệt.
    /// </summary>
    Task<FileComparisonResult> CompareAsync(
        string documentId1,
        string documentId2,
        CancellationToken ct = default);

    /// <summary>
    /// So sánh 2 stream (upload trực tiếp, không lưu DB).
    /// </summary>
    Task<FileComparisonResult> CompareStreamsAsync(
        Stream stream1, string fileName1, string contentType1,
        Stream stream2, string fileName2, string contentType2,
        CancellationToken ct = default);
}


