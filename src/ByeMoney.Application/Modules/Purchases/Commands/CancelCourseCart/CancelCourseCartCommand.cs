using ByeMoney.Application.Modules.Purchases.Cart.DTOs;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.CancelCourseCart;

public sealed record CancelCourseCartCommand(Guid CartId) : IRequest<Result<CartResponse>>;
