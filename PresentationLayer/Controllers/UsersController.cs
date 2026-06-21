using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;

namespace PresentationLayer.Controllers;

/// <summary>
/// REST API quản lý Người dùng (Users) — chỉ Admin mới được quyền truy cập các chức năng quản lý.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = "AdminOnly")]
public class UsersController : ControllerBase
{
    private readonly IUserService _service;

    public UsersController(IUserService service)
    {
        _service = service;
    }

    /// <summary>Lấy danh sách tất cả người dùng.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UserResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetAll()
    {
        var users = await _service.GetAllAsync();
        return Ok(users.Select(ToResponse));
    }

    /// <summary>Lấy thông tin người dùng theo Id.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetById(string id)
    {
        var user = await _service.GetByIdAsync(id);
        return user is null ? NotFound(new { message = "Không tìm thấy người dùng." }) : Ok(ToResponse(user));
    }

    /// <summary>Tạo người dùng mới.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserResponse>> Create([FromBody] UserCreateRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var (success, err, verificationToken) = await _service.CreateAsync(
            req.Username, req.Email, req.FullName, req.Password, req.Role);

        if (!success) return BadRequest(new { message = err ?? "Tạo người dùng thất bại." });

        // Tìm lại user vừa tạo để trả về response đầy đủ
        var users = await _service.GetAllAsync();
        var createdUser = users.FirstOrDefault(u => u.Username == req.Username);

        if (createdUser is null) return BadRequest(new { message = "Không tải được thông tin tài khoản vừa tạo." });

        return CreatedAtAction(nameof(GetById), new { id = createdUser.Id }, ToResponse(createdUser));
    }

    /// <summary>Cập nhật vai trò (Role) của người dùng.</summary>
    [HttpPut("{id}/role")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRole(string id, [FromBody] UserRoleUpdateRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var existing = await _service.GetByIdAsync(id);
        if (existing is null) return NotFound(new { message = "Không tìm thấy người dùng." });

        var (success, err) = await _service.UpdateRoleAsync(id, req.Role);
        if (!success) return BadRequest(new { message = err ?? "Cập nhật vai trò thất bại." });

        return Ok(new { message = "Cập nhật vai trò thành công." });
    }

    /// <summary>Cập nhật quyền upload tài liệu của người dùng.</summary>
    [HttpPut("{id}/upload-permission")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetUploadPermission(string id, [FromBody] UserUploadPermissionRequest req)
    {
        var existing = await _service.GetByIdAsync(id);
        if (existing is null) return NotFound(new { message = "Không tìm thấy người dùng." });

        var (success, err) = await _service.SetUploadPermissionAsync(id, req.CanUpload, req.SubjectId);
        if (!success) return BadRequest(new { message = err ?? "Cập nhật quyền upload thất bại." });

        return Ok(new { message = "Cập nhật quyền upload thành công." });
    }

    /// <summary>Reset mật khẩu của người dùng.</summary>
    [HttpPut("{id}/reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(string id, [FromBody] UserResetPasswordRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var existing = await _service.GetByIdAsync(id);
        if (existing is null) return NotFound(new { message = "Không tìm thấy người dùng." });

        var (success, err) = await _service.ResetPasswordAsync(id, req.NewPassword);
        if (!success) return BadRequest(new { message = err ?? "Đặt lại mật khẩu thất bại." });

        return Ok(new { message = "Đặt lại mật khẩu thành công." });
    }

    /// <summary>Xoá tài khoản người dùng.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id)
    {
        var existing = await _service.GetByIdAsync(id);
        if (existing is null) return NotFound(new { message = "Không tìm thấy người dùng." });

        var (success, err) = await _service.DeleteAsync(id);
        if (!success) return BadRequest(new { message = err ?? "Xoá người dùng thất bại." });

        return NoContent();
    }

    private static UserResponse ToResponse(ServiceLayer.DTOs.UserDto u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        Email = u.Email,
        FullName = u.FullName,
        Role = u.Role,
        CanUploadDocuments = u.CanUploadDocuments,
        AssignedSubjectId = u.AssignedSubjectId,
        AvatarPath = u.AvatarPath,
        Bio = u.Bio,
        IsEmailVerified = u.IsEmailVerified,
        CreatedAt = u.CreatedAt
    };
}
