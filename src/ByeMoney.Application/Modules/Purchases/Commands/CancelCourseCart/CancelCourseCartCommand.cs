using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.CancelCourseCart;

public sealed record CancelCourseCartCommand(Guid CartId) : IRequest<Result<CartResponse>>;
