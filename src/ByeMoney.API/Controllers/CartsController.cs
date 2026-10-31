using ByeMoney.API.Contracts.Carts;
using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Commands.CancelCart;
using ByeMoney.Application.Modules.Purchases.Commands.CreateCartTopUp;
using ByeMoney.Application.Modules.Purchases.Commands.PrepareCart;
using ByeMoney.Application.Modules.Purchases.Commands.PurchaseCart;
using ByeMoney.Application.Modules.Purchases.Queries.GetCart;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;
using ByeMoney.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[Authorize]
// مسیر عمومی سبد قرارداد بیرونی فرانت است؛ تغییر آن نیازمند هماهنگی است.
[Route("api/course-carts")]
public sealed class CartsController(ISender sender) : ControllerBase
{
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Prepare(
        [FromBody] PrepareCartRequest request,
        CancellationToken ct)
    {
        var command = new PrepareCartCommand(request.CartId, request.ExternalCourseIds, request.CouponCode, request.Items);
        var result = await sender.Send(command, ct);
        return Respond(result);
    }

    [HttpGet("{cartId:guid}")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromRoute] Guid cartId,
        CancellationToken ct)
    {
        var query = new GetCartQuery(cartId);
        var result = await sender.Send(query, ct);
        return Respond(result);
    }

    [HttpPost("{cartId:guid}/purchase")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Purchase(
        [FromRoute] Guid cartId,
        CancellationToken ct)
    {
        var command = new PurchaseCartCommand(cartId);
        var result = await sender.Send(command, ct);
        return Respond(result);
    }

    [HttpPost("{cartId:guid}/cancel")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(
        [FromRoute] Guid cartId,
        CancellationToken ct)
    {
        var command = new CancelCartCommand(cartId);
        var result = await sender.Send(command, ct);
        return Respond(result);
    }

    [HttpPost("{cartId:guid}/topup")]
    [ProducesResponseType(typeof(CreateTopUpRequestResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> TopUp(
        [FromRoute] Guid cartId,
        CancellationToken ct)
    {
        var command = new CreateCartTopUpCommand(cartId);
        var result = await sender.Send(command, ct);
        return Respond(result);
    }

    private IActionResult Respond<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        var statusCode = result.ErrorCode == CartErrorCodes.Unavailable
            ? StatusCodes.Status503ServiceUnavailable
            : result.Status switch
            {
                ResultStatus.NotFound => StatusCodes.Status404NotFound,
                ResultStatus.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            };

        return StatusCode(statusCode, new CartErrorResponse(
            result.ErrorCode ?? CartErrorCodes.Invalid,
            result.ErrorMessage!));
    }
}
