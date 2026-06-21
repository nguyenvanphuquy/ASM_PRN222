using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PresentationLayer.Hubs;

// Hub realtime cho danh sách Môn học.
// Khi Admin thêm/sửa/xoá môn học qua REST API, server broadcast sự kiện
// "SubjectsChanged" tới mọi client đang mở trang Subjects để cập nhật
// ngay lập tức mà KHÔNG cần reload trang (yêu cầu Milestone 3).
[Authorize]
public class SubjectsHub : Hub
{
}
