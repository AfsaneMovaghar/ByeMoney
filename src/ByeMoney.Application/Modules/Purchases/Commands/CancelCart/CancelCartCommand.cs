using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.CancelCart;

public sealed record CancelCartCommand(Guid CartId) : IRequest<Result<CartResponse>>;
