using ByeMoney.Application.Modules.Purchases.Cart.DTOs;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetCourseCart;

public sealed record GetCourseCartQuery(Guid CartId) : IRequest<Result<CartResponse>>;
