using ByeMoney.Application.Modules.Purchases.Commands.ConfirmMyPhysicalDelivery;
using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Queries.GetMyOrder;
using ByeMoney.Application.Modules.Purchases.Queries.GetMyOrderReceipt;
using ByeMoney.Application.Modules.Purchases.Queries.GetMyOrders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[Route("api/me/orders")]
[Authorize]
public sealed class MyOrdersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PurchasePage<OrderListItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PurchasePage<OrderListItem>>> List(
        [FromQuery] OrderFilter filter, CancellationToken ct)
    {
        return Ok(await sender.Send(new GetMyOrdersQuery(filter), ct));
    }

    [HttpGet("{orderId}")]
    [ProducesResponseType(typeof(MyOrderDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MyOrderDetails>> Details(string orderId, CancellationToken ct)
    {
        return Ok(await sender.Send(new GetMyOrderQuery(orderId), ct));
    }

    [HttpGet("{orderId}/receipt")]
    [ProducesResponseType(typeof(OrderReceipt), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderReceipt>> Receipt(string orderId, CancellationToken ct)
    {
        return Ok(await sender.Send(new GetMyOrderReceiptQuery(orderId), ct));
    }

    /// <summary>
    /// تأیید دریافت کالای ارسال‌شده توسط خریدار؛ فقط سفارش خودش، تکرار بی‌اثر.
    /// </summary>
    [HttpPost("{orderId}/confirm-delivery")]
    [ProducesResponseType(typeof(PhysicalDeliveryConfirmationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PhysicalDeliveryConfirmationResponse>> ConfirmDelivery(
        string orderId, CancellationToken ct)
    {
        return Ok(await sender.Send(new ConfirmMyPhysicalDeliveryCommand(orderId), ct));
    }
}
