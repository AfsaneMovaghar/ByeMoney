using ByeMoney.Application.Modules.Wallet.Queries.GetWalletBalance;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[Route("api/wallet")]
[Authorize]
public class WalletController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    /// <summary>
    /// Returns the current authenticated user's Noor wallet balance.
    /// </summary>
    [HttpGet("balance")]
    public async Task<ActionResult<WalletBalanceResponse>> GetBalance(CancellationToken ct)
    {
        var response = await _sender.Send(new GetWalletBalanceQuery(), ct);
        return Ok(response);
    }
}

