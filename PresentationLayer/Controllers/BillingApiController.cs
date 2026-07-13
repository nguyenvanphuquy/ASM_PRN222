using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceLayer.Dtos;
using ServiceLayer.Services.Interfaces;
using System.Security.Claims;

namespace PresentationLayer.Controllers;

/// <summary>API số dư token của user đang đăng nhập (phục vụ badge realtime).</summary>
[ApiController]
[Route("api/billing")]
[Authorize]
[Produces("application/json")]
public class BillingApiController : ControllerBase
{
    private readonly IBillingService _billing;

    public BillingApiController(IBillingService billing) => _billing = billing;

    [HttpGet("balance")]
    [ProducesResponseType(typeof(TokenBalance), StatusCodes.Status200OK)]
    public async Task<ActionResult<TokenBalance>> GetBalance()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
        if (role is "Admin" or "Lecturer")
            return Ok(new TokenBalance(0, 0, -1, null)); // -1 = không giới hạn

        await _billing.EnsureFreeGrantAsync(userId);
        return Ok(await _billing.GetBalanceAsync(userId));
    }
}
