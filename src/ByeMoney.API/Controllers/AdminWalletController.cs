using ByeMoney.API.Contracts.Wallet;
using ByeMoney.Application.Modules.Wallet.Queries.GetBatchWalletBalances;
using ByeMoney.Domain.Modules.Identity.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[Route("api/admin/wallets")]
[Authorize(Policy = PolicyNames.RequireWalletView)]
public sealed class AdminWalletController(ISender sender) : ControllerBase
{
    /// <summary>
    /// ??????? ????????? ?????? ??????? ??? ??????? ??? ??????.
    /// </summary>
    [HttpPost("batch-balances")]
    [ProducesResponseType(typeof(BatchWalletBalancesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BatchWalletBalancesResponse>> GetBatchBalances(
        [FromBody] BatchWalletBalancesRequest request, CancellationToken ct)
    {
        var response = await sender.Send(new GetBatchWalletBalancesQuery(request.UserIds), ct);
        return Ok(response);
    }
}
