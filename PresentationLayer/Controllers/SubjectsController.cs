using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using PresentationLayer.Hubs;
using ServiceLayer.Dtos;
using ServiceLayer.DTOs;
using ServiceLayer.Services.Interfaces;

namespace PresentationLayer.Controllers;

/// <summary>
/// REST API quản lý Môn học (Subjects) — đầy đủ CRUD.
/// Mọi thay đổi (POST/PUT/DELETE) được broadcast qua SignalR để các client
/// (sinh viên, giảng viên) tự cập nhật danh sách mà không cần tải lại trang.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class SubjectsController : ControllerBase
{
    private readonly ISubjectService _service;
    private readonly IHubContext<SubjectsHub> _hub;

    public SubjectsController(ISubjectService service, IHubContext<SubjectsHub> hub)
    {
        _service = service;
        _hub = hub;
    }

    /// <summary>Lấy danh sách tất cả môn học.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SubjectResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SubjectResponse>>> GetAll()
    {
        var subjects = await _service.GetAllAsync();
        return Ok(subjects.Select(ToResponse));
    }

    /// <summary>Lấy chi tiết một môn học theo Id.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SubjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubjectResponse>> GetById(string id)
    {
        var subject = await _service.GetByIdAsync(id);
        return subject is null ? NotFound(new { message = "Không tìm thấy môn học." }) : Ok(ToResponse(subject));
    }

    /// <summary>Tạo môn học mới (chỉ Admin).</summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(SubjectResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SubjectResponse>> Create([FromBody] SubjectRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        await _service.CreateAsync(req.Code, req.Name, req.Description);

        // ISubjectService.CreateAsync của base trả về void → tải lại theo Code để lấy bản ghi vừa tạo.
        var all = await _service.GetAllAsync();
        var created = all.FirstOrDefault(s => s.Code == req.Code);
        if (created is null) return BadRequest(new { message = "Không tải được thông tin môn học vừa tạo." });

        await BroadcastAsync("created", created);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ToResponse(created));
    }

    /// <summary>Cập nhật môn học (chỉ Admin).</summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(SubjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubjectResponse>> Update(string id, [FromBody] SubjectRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var existing = await _service.GetByIdAsync(id);
        if (existing is null) return NotFound(new { message = "Không tìm thấy môn học." });

        await _service.UpdateAsync(id, req.Code, req.Name, req.Description);

        var updated = await _service.GetByIdAsync(id);
        if (updated is null) return NotFound(new { message = "Không tìm thấy môn học." });

        await BroadcastAsync("updated", updated);
        return Ok(ToResponse(updated));
    }

    /// <summary>Xoá môn học (chỉ Admin).</summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id)
    {
        var existing = await _service.GetByIdAsync(id);
        if (existing is null) return NotFound(new { message = "Không tìm thấy môn học." });

        await _service.DeleteAsync(id);
        await _hub.Clients.All.SendAsync("SubjectsChanged", new { action = "deleted", id });
        return NoContent();
    }

    // Gửi sự kiện realtime tới mọi client đang lắng nghe.
    private Task BroadcastAsync(string action, SubjectDto s)
        => _hub.Clients.All.SendAsync("SubjectsChanged", new { action, subject = ToResponse(s) });

    private static SubjectResponse ToResponse(SubjectDto s) => new()
    {
        Id = s.Id,
        Code = s.Code,
        Name = s.Name,
        Description = s.Description,
        CreatedAt = s.CreatedAt
    };
}
