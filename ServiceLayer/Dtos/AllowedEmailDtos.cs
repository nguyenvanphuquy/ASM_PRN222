using System.ComponentModel.DataAnnotations;

namespace ServiceLayer.Dtos;

public class AllowedEmailRequest
{
    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    [StringLength(200, ErrorMessage = "Email tối đa 200 ký tự")]
    public string Email { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "Ghi chú tối đa 300 ký tự")]
    public string Note { get; set; } = string.Empty;
}
