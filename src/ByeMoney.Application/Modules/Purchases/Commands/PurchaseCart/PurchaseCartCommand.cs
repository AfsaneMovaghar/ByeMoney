using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.PurchaseCart;

public sealed record PurchaseCartCommand(Guid CartId) : IRequest<Result<CartResponse>>;
