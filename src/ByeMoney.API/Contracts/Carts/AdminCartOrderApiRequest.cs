using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.API.Contracts.Carts;

/// <summary>
/// ورودی سفارش دستی ادمین؛ کوپن عمداً جزو قرارداد نیست.
/// </summary>
public sealed record AdminCartOrderApiRequest(
    Guid CartId,
    string BeneficiaryExternalUserId,
    CartItemInput[]? Items,
    bool IsFree,
    string? FreeReason);
