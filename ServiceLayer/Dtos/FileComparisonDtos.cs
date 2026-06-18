namespace ServiceLayer.Dtos;

public enum DiffType
{
    ContentError,      // Lỗi nội dung (số liệu sai, tên sai, sự kiện sai)
    Typo,              // Lỗi chính tả
    MissingContent,    // Nội dung bị thiếu ở một file
    ExtraContent,      // Nội dung thừa/dư ở một file
    FormatDifference,  // Khác biệt về định dạng/cấu trúc
    Other              // Khác biệt khác
}

public record ComparisonDifference(
    int Number,
    string Location,     // Vị trí trong tài liệu (trang, đoạn...)
    string Description,  // Mô tả chi tiết sự khác biệt
    DiffType Type,
    string? File1Excerpt, // Đoạn trích từ file 1 (nếu có)
    string? File2Excerpt  // Đoạn trích từ file 2 (nếu có)
);

public record FileComparisonResult(
    string DocumentId1,
    string FileName1,
    string DocumentId2,
    string FileName2,
    string Summary,
    int TotalDifferences,
    List<ComparisonDifference> Differences
);
