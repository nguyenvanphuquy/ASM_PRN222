using System.ComponentModel.DataAnnotations;

namespace ServiceLayer.Dtos;

public class FeedbackRequest
{
    [Required(ErrorMessage = "Đánh giá không được để trống")]
    [Range(1, 5, ErrorMessage = "Đánh giá từ 1 đến 5 sao")]
    public int Rating { get; set; }

    [Required(ErrorMessage = "Nội dung phản hồi không được để trống")]
    [StringLength(2000, ErrorMessage = "Nội dung tối đa 2000 ký tự")]
    public string Content { get; set; } = string.Empty;
}

public class FeedbackReplyRequest
{
    [Required(ErrorMessage = "Nội dung câu trả lời không được để trống")]
    [StringLength(2000, ErrorMessage = "Nội dung tối đa 2000 ký tự")]
    public string Content { get; set; } = string.Empty;
}

public class FeedbackResponse
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? UserAvatar { get; set; }
    public int Rating { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<FeedbackReplyResponse> Replies { get; set; } = new();
}

public class FeedbackReplyResponse
{
    public string Id { get; set; } = string.Empty;
    public string FeedbackId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? UserAvatar { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
}
