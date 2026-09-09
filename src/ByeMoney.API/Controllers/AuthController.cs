using ByeMoney.API.Resources;
using ByeMoney.Application.Modules.Identity.Users.Commands.SyncUserFromStrapi;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        var externalUserId = User.FindFirst("documentId")?.Value;

        if (string.IsNullOrWhiteSpace(externalUserId))
            throw new UnauthorizedAccessException(ApiErrors.Auth_DocumentIdClaimMissing);

        var cmd = new SyncUserFromStrapiCommand(externalUserId);
        var userId = await _sender.Send(cmd, ct);
        return Ok(userId);
    }
}
