using ByeMoney.Application.Modules.Purchases.Cart.DTOs;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourseCart;

public sealed record PurchaseCourseCartCommand(Guid CartId) : IRequest<Result<CartResponse>>;
