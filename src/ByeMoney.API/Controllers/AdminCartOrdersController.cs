using ByeMoney.API.Contracts.Carts;
using ByeMoney.API.Contracts.Purchases;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Commands.AdminCartOrder;
using ByeMoney.Application.Modules.Purchases.Commands.CreatePhysicalShipment;
using ByeMoney.Domain.Modules.Identity.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[Route("api/admin/carts")]
[Authorize(Policy = PolicyNames.RequireCoursesManage)]
public sealed class AdminCartOrdersController(ISender sender) : ControllerBase
{
    /// <summary>
    /// ثبت دستی سفارش دوره و فصل روی سبد بای‌مانی به‌نام کاربر و پرداخت فوری از کیف او، یا ثبت رایگان با علت.
    /// </summary>
    [HttpPost("orders")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(CartErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(CartErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(CartErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create(
        [FromBody] AdminCartOrderApiRequest request,
        CancellationToken ct)
    {
        var command = new AdminCartOrderCommand(
            request.CartId,
            request.BeneficiaryExternalUserId,
            request.Items,
            request.IsFree,
            request.FreeReason);
        var result = await sender.Send(command, ct);
        return CartHttpResults.From(result);
    }

    /// <summary>
    /// ساخت یک مرسوله نزد واسط پستی برای همهٔ اقلام فیزیکیِ فعال این سبد.
    /// «ارسال‌شده» فقط با کد پستی واقعی ثبت می‌شود.
    /// </summary>
    [HttpPost("{cartId:guid}/shipments")]
    [ProducesResponseType(typeof(CreatePhysicalShipmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreatePhysicalShipmentResponse>> CreateShipment(
        Guid cartId,
        [FromBody] CreatePhysicalShipmentApiRequest? request,
        CancellationToken ct)
    {
        return Ok(await sender.Send(
            new CreatePhysicalShipmentCommand(
                cartId, request?.CollectionType, request?.Provider, request?.BoxTypeId), ct));
    }
}
