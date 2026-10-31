using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetCart;

public sealed record GetCartQuery(Guid CartId) : IRequest<Result<CartResponse>>;
