using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;
using System.Security.Claims;

namespace PresentationLayer.Controllers;

/// <summary>
/// REST API quản lý Danh sách Email được phép đăng ký (Whitelist) — Chỉ Admin.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = "AdminOnly")]
public class AllowedEmailsController : ControllerBase
{
    private readonly IAllowedEmailService _service;

    public AllowedEmailsController(IAllowedEmailService service)
    {
        _service = service;
    }

    /// <summary>Lấy danh sách tất cả các email nằm trong danh sách được phép.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AllowedEmail>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AllowedEmail>>> GetAll()
    {
        var list = await _service.GetAllAsync();
        return Ok(list);
    }

    /// <summary>Thêm email mới vào danh sách được phép đăng ký.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Add([FromBody] AllowedEmailRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var currentUser = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "Admin";
        var (success, err) = await _service.AddAsync(req.Email, req.Note, currentUser);

        if (!success) return BadRequest(new { message = err ?? "Thêm email thất bại." });

        return CreatedAtAction(nameof(GetAll), null, new { message = "Thêm email vào danh sách thành công." });
    }

    /// <summary>Xoá email khỏi danh sách được phép đăng ký.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id)
    {
        var list = await _service.GetAllAsync();
        var exists = list.Any(e => e.Id == id);
        if (!exists) return NotFound(new { message = "Không tìm thấy email cần xoá trong danh sách." });

        await _service.DeleteAsync(id);
        return NoContent();
    }
}
