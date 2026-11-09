using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyOrder;

public sealed record GetMyOrderQuery(string OrderId) : IRequest<MyOrderDetails>;
