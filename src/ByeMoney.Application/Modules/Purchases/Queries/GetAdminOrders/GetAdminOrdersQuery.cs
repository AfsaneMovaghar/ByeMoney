using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminOrders;

public sealed record GetAdminOrdersQuery(AdminOrderFilter Filter)
    : IRequest<PurchasePage<AdminOrderListItem>>;
