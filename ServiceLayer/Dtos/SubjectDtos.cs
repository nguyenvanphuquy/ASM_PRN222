using System.ComponentModel.DataAnnotations;

namespace ServiceLayer.Dtos;

// Dữ liệu client gửi lên khi tạo / cập nhật môn học qua REST API.
public class SubjectRequest
{
    [Required(ErrorMessage = "Mã môn không được để trống")]
    [StringLength(20, ErrorMessage = "Mã môn tối đa 20 ký tự")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên môn không được để trống")]
    [StringLength(300, ErrorMessage = "Tên môn tối đa 300 ký tự")]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

// Dữ liệu API trả về cho client.
public class SubjectResponse
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
