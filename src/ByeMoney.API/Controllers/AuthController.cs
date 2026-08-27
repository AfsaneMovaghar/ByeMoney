using MediatR;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using ByeMoney.Application.Modules.Identity.Users.Commands.SyncUserFromStrapi;

namespace ByeMoney.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    [Authorize]
    [HttpPost("sync")]
    public async Task<IActionResult> Sync(CancellationToken ct)
    {
        var idClaim = User.Claims.FirstOrDefault(c => c.Type == "id")?.Value;
        if (string.IsNullOrEmpty(idClaim) || !int.TryParse(idClaim, out var strapiUserId))
            return BadRequest();

        var cmd = new SyncUserFromStrapiCommand(strapiUserId);
        var userId = await _sender.Send(cmd, ct);
        return Ok(userId);
    }
}
