using ByeMoney.Application.Modules.Purchases.Cart.DTOs;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.PrepareCourseCart;

public sealed record PrepareCourseCartCommand(
    Guid CartId,
    string[] ExternalCourseIds,
    string? CouponCode) : IRequest<Result<CartResponse>>;
