using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyOrders;

public sealed record GetMyOrdersQuery(OrderFilter Filter)
    : IRequest<PurchasePage<OrderListItem>>;
