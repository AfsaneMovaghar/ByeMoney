using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminOrderReceipt;

public sealed record GetAdminOrderReceiptQuery(string OrderId) : IRequest<OrderReceipt>;
