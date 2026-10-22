using ByeMoney.API.Contracts.CourseCarts;
using ByeMoney.Application.Modules.Purchases.Cart.Constants;
using ByeMoney.Application.Modules.Purchases.Cart.DTOs;
using ByeMoney.Application.Modules.Purchases.Commands.CancelCourseCart;
using ByeMoney.Application.Modules.Purchases.Commands.CreateCourseCartTopUp;
using ByeMoney.Application.Modules.Purchases.Commands.PrepareCourseCart;
using ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourseCart;
using ByeMoney.Application.Modules.Purchases.Queries.GetCourseCart;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;
using ByeMoney.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[Authorize]
[Route("api/course-carts")]
public sealed class CourseCartsController(ISender sender) : ControllerBase
{
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Prepare(
        [FromBody] PrepareCourseCartRequest request,
        CancellationToken ct)
    {
        var command = new PrepareCourseCartCommand(request.CartId, request.ExternalCourseIds, request.CouponCode, request.Items);
        var result = await sender.Send(command, ct);
        return Respond(result);
    }

    [HttpGet("{cartId:guid}")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromRoute] Guid cartId,
        CancellationToken ct)
    {
        var query = new GetCourseCartQuery(cartId);
        var result = await sender.Send(query, ct);
        return Respond(result);
    }

    [HttpPost("{cartId:guid}/purchase")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Purchase(
        [FromRoute] Guid cartId,
        CancellationToken ct)
    {
        var command = new PurchaseCourseCartCommand(cartId);
        var result = await sender.Send(command, ct);
        return Respond(result);
    }

    [HttpPost("{cartId:guid}/cancel")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(
        [FromRoute] Guid cartId,
        CancellationToken ct)
    {
        var command = new CancelCourseCartCommand(cartId);
        var result = await sender.Send(command, ct);
        return Respond(result);
    }

    [HttpPost("{cartId:guid}/topup")]
    [ProducesResponseType(typeof(CreateTopUpRequestResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> TopUp(
        [FromRoute] Guid cartId,
        CancellationToken ct)
    {
        var command = new CreateCourseCartTopUpCommand(cartId);
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

        return StatusCode(statusCode, new CourseCartErrorResponse(
            result.ErrorCode ?? CartErrorCodes.Invalid,
            result.ErrorMessage!));
    }
}
