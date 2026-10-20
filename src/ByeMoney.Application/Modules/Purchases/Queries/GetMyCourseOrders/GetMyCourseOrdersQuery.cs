using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyCourseOrders;

public sealed record GetMyCourseOrdersQuery(CourseOrderFilter Filter)
    : IRequest<PurchasePage<CourseOrderListItem>>;
