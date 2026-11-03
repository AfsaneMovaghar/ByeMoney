using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Resources;

namespace ByeMoney.Domain.Modules.Purchases;

/// <summary>
/// سهم نور عادی و هدیهٔ پرداخت واقعی یک قلم؛ مبنای برگشت مجاز در D09.
/// </summary>
public readonly record struct PaymentShares(decimal RegularInNoor, decimal GiftInNoor)
{
    public decimal TotalInNoor => RegularInNoor + GiftInNoor;

    public void EnsureMatches(decimal paidAmountInNoor)
    {
        if (RegularInNoor < 0 || GiftInNoor < 0 || TotalInNoor != paidAmountInNoor)
        {
            throw new DomainException(DomainErrors.PaymentShares_Invalid);
        }
    }
}
