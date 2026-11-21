using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Commands.CreatePhysicalShipment;

/// <summary>
/// تجمیع اقلام فعال یک سبد در یک مرسوله. وزن و ارزش فریزشده برای تعداد اصلی ردیف است
/// و اینجا با تعداد باقی‌مانده مقیاس می‌شود. ابعاد از جدول جعبه می‌آید، نه از بدنهٔ درخواست.
/// </summary>
internal static class CartShipmentParcel
{
    public static ShipmentParcelDetails Build(
        Guid cartId,
        IReadOnlyList<PhysicalProductPurchase> activeItems,
        int? boxTypeId)
    {
        var baseBox = activeItems.Max(item => item.Parcel!.BoxTypeId);
        var unitCount = activeItems.Sum(item => item.RemainingQuantity);
        var chosenBox = PostexStandardBoxes.ShipmentBoxTypeId(baseBox, unitCount, boxTypeId);
        if (!PostexStandardBoxes.TryGet(chosenBox, out var size))
        {
            throw new DomainException(OrderErrors.InvalidBoxTypeId);
        }

        var items = new List<ShipmentParcelItem>(activeItems.Count);
        long totalWeight = 0;
        long totalValue = 0;
        var fragile = false;

        foreach (var purchase in activeItems)
        {
            var facts = purchase.Parcel!;
            var weight = Scale(facts.WeightGrams, purchase.RemainingQuantity, purchase.Quantity);
            var value = Scale(facts.DeclaredValueIrr, purchase.RemainingQuantity, purchase.Quantity);
            items.Add(new ShipmentParcelItem(
                purchase.ProductTitle, purchase.RemainingQuantity, weight, value));
            totalWeight += weight;
            totalValue += value;
            if (facts.IsFragile)
            {
                fragile = true;
            }
        }

        if (totalWeight > int.MaxValue)
        {
            throw new DomainException(OrderErrors.ShipmentScaledParcelInvalid);
        }

        return new ShipmentParcelDetails(
            $"BM-{cartId:N}",
            items,
            chosenBox,
            size.LengthCm,
            size.WidthCm,
            size.HeightCm,
            (int)totalWeight,
            totalValue,
            fragile);
    }

    private static int Scale(int total, int remaining, int original)
    {
        var scaled = Scale((long)total, remaining, original);
        if (scaled > int.MaxValue)
        {
            throw new DomainException(OrderErrors.ShipmentScaledParcelInvalid);
        }

        return (int)scaled;
    }

    private static long Scale(long total, int remaining, int original)
    {
        if (original < 1 || remaining < 1 || remaining > original || total < 1)
        {
            throw new DomainException(OrderErrors.ShipmentScaledParcelInvalid);
        }

        var scaled = total * remaining / original;
        if (scaled < 1)
        {
            throw new DomainException(OrderErrors.ShipmentScaledParcelInvalid);
        }

        return scaled;
    }
}
