using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Purchases;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.PrepareCart;

public sealed record PrepareCartCommand(
    Guid CartId,
    CartItemInput[]? Items = null,
    string? CouponCode = null) : IRequest<Result<CartResponse>>;
