using ByeMoney.Application.Modules.Purchases.Cart;
using ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;
using ByeMoney.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

public sealed record PrepareCourseCartRequest(Guid CartId, string[] ExternalCourseIds, string? CouponCode);
public sealed record CourseCartErrorResponse(string Code, string Error);

[ApiController, Authorize, Route("api/course-carts")]
public sealed class CourseCartsController(ISender sender) : ControllerBase
{
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Prepare(PrepareCourseCartRequest request, CancellationToken ct) =>
        Respond(await sender.Send(new PrepareCourseCartCommand(request.CartId, request.ExternalCourseIds, request.CouponCode), ct));
    [HttpGet("{cartId:guid}")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid cartId, CancellationToken ct) => Respond(await sender.Send(new GetCourseCartQuery(cartId), ct));
    [HttpPost("{cartId:guid}/purchase")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Purchase(Guid cartId, CancellationToken ct) => Respond(await sender.Send(new PurchaseCourseCartCommand(cartId), ct));
    [HttpPost("{cartId:guid}/cancel")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid cartId, CancellationToken ct) => Respond(await sender.Send(new CancelCourseCartCommand(cartId), ct));
    [HttpPost("{cartId:guid}/topup")]
    [ProducesResponseType(typeof(CreateTopUpRequestResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> TopUp(Guid cartId, CancellationToken ct) => Respond(await sender.Send(new CreateCourseCartTopUpCommand(cartId), ct));
    private IActionResult Respond<T>(Result<T> result)
    {
        if (result.IsSuccess) return Ok(result.Value);
        var status = result.ErrorCode == CartErrorCodes.Unavailable ? 503 : result.Status switch
        { ResultStatus.NotFound => 404, ResultStatus.Conflict => 409, _ => 400 };
        return StatusCode(status, new CourseCartErrorResponse(result.ErrorCode ?? CartErrorCodes.Invalid, result.ErrorMessage!));
    }
}
