using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyOrderReceipt;

public sealed record GetMyOrderReceiptQuery(string OrderId) : IRequest<OrderReceipt>;
