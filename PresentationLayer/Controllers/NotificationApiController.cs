using DataAccessLayer.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
[Produces("application/json")]
public class NotificationApiController : ControllerBase
{
    private readonly AppDbContext _db;
    public NotificationApiController(AppDbContext db) => _db = db;

    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    [HttpGet]
    public async Task<IActionResult> GetMyNotifications()
    {
        var notifs = await _db.Notifications
            .Where(n => n.UserId == UserId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(20)
            .Select(n => new
            {
                n.Id,
                n.Type,
                n.Title,
                n.Message,
                n.IsRead,
                Time = n.CreatedAt.ToLocalTime().ToString("HH:mm dd/MM")
            })
            .ToListAsync();

        return Ok(notifs);
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var unread = await _db.Notifications
            .Where(n => n.UserId == UserId && !n.IsRead)
            .ToListAsync();

        foreach (var n in unread) n.IsRead = true;
        
        if (unread.Any()) await _db.SaveChangesAsync();
        
        return NoContent();
    }
}
