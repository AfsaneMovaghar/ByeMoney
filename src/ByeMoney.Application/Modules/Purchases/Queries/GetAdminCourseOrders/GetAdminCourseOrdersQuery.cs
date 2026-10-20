using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminCourseOrders;

public sealed record GetAdminCourseOrdersQuery(AdminCourseOrderFilter Filter)
    : IRequest<PurchasePage<AdminCourseOrderListItem>>;
