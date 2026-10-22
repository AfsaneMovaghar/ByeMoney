using ByeMoney.Application.Modules.Purchases.Cart.DTOs;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Purchases;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.PrepareCourseCart;

public sealed record PrepareCourseCartCommand(
    Guid CartId,
    string[]? ExternalCourseIds = null,
    string? CouponCode = null,
    CartItemInput[]? Items = null) : IRequest<Result<CartResponse>>;
