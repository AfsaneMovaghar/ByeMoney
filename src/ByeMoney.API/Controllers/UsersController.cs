using MediatR;
using Microsoft.AspNetCore.Mvc;
using ByeMoney.Application.Modules.Identity.Users.Commands.CreateUser;

namespace ByeMoney.API.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserCommand command, CancellationToken ct)
    {
        var userId = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(Create), new { id = userId.Value }, userId);
    }
}