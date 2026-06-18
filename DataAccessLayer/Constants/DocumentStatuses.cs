namespace DataAccessLayer.Constants;

/// <summary>
/// Vòng đời trạng thái của một tài liệu khi được index:
/// Pending → Processing → Indexed (hoặc Empty/Failed). Giống pipeline của Assignment 1.
/// </summary>
public static class DocumentStatuses
{
    public const string Pending = "Pending";       // đã nhận, chờ xử lý
    public const string Processing = "Processing"; // đang trích xuất + chunk
    public const string Indexed = "Indexed";       // đã chunk thành công, sẵn sàng cho chatbot
    public const string Empty = "Empty";           // xử lý xong nhưng không tách được chunk nào
    public const string Failed = "Failed";         // trích xuất/chunk lỗi
}
