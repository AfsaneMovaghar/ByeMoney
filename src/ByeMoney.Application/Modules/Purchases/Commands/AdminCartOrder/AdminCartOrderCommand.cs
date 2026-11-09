using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Purchases;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.AdminCartOrder;

/// <summary>
/// سفارش دستی ادمین برای دوره و فصل روی سبد بای‌مانی به‌نام کاربر ذینفع.
/// </summary>
public sealed record AdminCartOrderCommand(
    Guid CartId,
    string BeneficiaryExternalUserId,
    CartItemInput[]? Items,
    bool IsFree,
    string? FreeReason) : IRequest<Result<CartResponse>>;
