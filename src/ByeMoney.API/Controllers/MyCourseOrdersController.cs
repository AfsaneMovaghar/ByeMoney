using ByeMoney.Application.Modules.Purchases.Queries.History;
using ByeMoney.Application.Modules.Purchases.Queries.Orders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[Route("api/me/course-orders")]
[Authorize]
public sealed class MyCourseOrdersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PurchasePage<CourseOrderListItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PurchasePage<CourseOrderListItem>>> List(
        [FromQuery] CourseOrderFilter filter, CancellationToken ct)
    {
        return Ok(await sender.Send(new GetMyCourseOrdersQuery(filter), ct));
    }

    [HttpGet("{orderId}")]
    [ProducesResponseType(typeof(CourseOrderDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseOrderDetails>> Details(string orderId, CancellationToken ct)
    {
        return Ok(await sender.Send(new GetMyCourseOrderQuery(orderId), ct));
    }
}
