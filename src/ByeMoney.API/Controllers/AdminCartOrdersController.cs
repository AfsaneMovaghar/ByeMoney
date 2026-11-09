using ByeMoney.API.Contracts.Carts;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Commands.AdminCartOrder;
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
}
