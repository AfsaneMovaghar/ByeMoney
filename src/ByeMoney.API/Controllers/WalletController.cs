using ByeMoney.Application.Modules.Wallet.Queries.GetWalletBalance;
using ByeMoney.Application.Modules.Wallet.Queries.GetWalletTransactions;
using ByeMoney.Application.Modules.Wallet.Transactions.DTOs;
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

    /// <summary>
    /// Returns the current authenticated user's wallet transactions (newest first).
    /// </summary>
    [HttpGet("transactions")]
    public async Task<ActionResult<WalletTransactionPage<WalletTransactionItem>>> GetTransactions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var response = await _sender.Send(new GetWalletTransactionsQuery(page, pageSize), ct);
        return Ok(response);
    }
}

