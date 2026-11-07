using ByeMoney.API.Contracts.Purchases;
using ByeMoney.Application.Modules.Purchases.Commands.CloseOrderSettlementPeriod;
using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Queries.GetOrderSettlementPeriodOrders;
using ByeMoney.Application.Modules.Purchases.Queries.GetOrderSettlementPeriods;
using ByeMoney.Application.Modules.Purchases.Queries.GetOrderSettlementPreview;
using ByeMoney.Application.Modules.Purchases.Settlements.DTOs;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Identity.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

/// <summary>
/// دوره‌های تسویهٔ دستی سفارش‌های بای‌مانی بین ادمین‌ها (D10). فقط تگ گزارشی است و هیچ نوری جابه‌جا نمی‌کند.
/// </summary>
[ApiController]
[Route("api/admin/course-order-settlements")]
[Authorize]
public sealed class AdminCourseOrderSettlementsController(ISender sender) : ControllerBase
{
    [HttpGet("preview")]
    [Authorize(Policy = PolicyNames.RequirePurchasesView)]
    [ProducesResponseType(typeof(OrderSettlementPreviewResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderSettlementPreviewResponse>> Preview(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Ok(await sender.Send(new GetOrderSettlementPreviewQuery(page, pageSize), ct));
    }

    [HttpPost]
    [Authorize(Policy = PolicyNames.RequirePurchasesSettle)]
    [ProducesResponseType(typeof(OrderSettlementPeriodResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(OrderSettlementErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Close(
        [FromBody] CloseOrderSettlementPeriodApiRequest request, CancellationToken ct)
    {
        var command = new CloseOrderSettlementPeriodCommand(request.Title, request.Note, request.PreviewToken);
        var result = await sender.Send(command, ct);

        if (result.IsSuccess)
        {
            return StatusCode(StatusCodes.Status201Created, result.Value);
        }

        // هندلر فقط نتیجهٔ Conflict با کد ساختاریافته برمی‌گرداند؛ هر حالت دیگری قرارداد را نقض کرده است.
        if (result.Status != ResultStatus.Conflict || result.ErrorCode is null)
        {
            throw new InvalidOperationException("Unexpected settlement close result.");
        }

        return Conflict(new OrderSettlementErrorResponse(result.ErrorCode, result.ErrorMessage!));
    }

    [HttpGet]
    [Authorize(Policy = PolicyNames.RequirePurchasesView)]
    [ProducesResponseType(typeof(PurchasePage<OrderSettlementPeriodResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PurchasePage<OrderSettlementPeriodResponse>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Ok(await sender.Send(new GetOrderSettlementPeriodsQuery(page, pageSize), ct));
    }

    [HttpGet("{periodId:guid}/orders")]
    [Authorize(Policy = PolicyNames.RequirePurchasesView)]
    [ProducesResponseType(typeof(PurchasePage<SettlementOrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchasePage<SettlementOrderResponse>>> Orders(
        Guid periodId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Ok(await sender.Send(new GetOrderSettlementPeriodOrdersQuery(periodId, page, pageSize), ct));
    }
}
