using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Resources;

namespace ByeMoney.Domain.Modules.Purchases;

public class PhysicalProductPurchase : BaseEntity<PhysicalProductPurchaseId>
{
    public Guid CartId { get; private set; }
    public UserId BuyerId { get; private set; }
    public string ExternalProductId { get; private set; } = null!;
    public string ProductTitle { get; private set; } = null!;
    public int Quantity { get; private set; }
    public decimal UnitPriceInNoor { get; private set; }
    public decimal PaidAmountInNoor { get; private set; }

    /// <summary>سهم نور عادی پرداخت واقعی این قلم؛ برای خریدهای پیش از ثبت سهم null است.</summary>
    public decimal? RegularAmountInNoor { get; private set; }

    /// <summary>سهم نور هدیهٔ پرداخت واقعی این قلم؛ برای خریدهای پیش از ثبت سهم null است.</summary>
    public decimal? GiftAmountInNoor { get; private set; }
    public Guid? LedgerTransactionId { get; private set; }
    public PhysicalDeliveryStatus DeliveryStatus { get; private set; } = PhysicalDeliveryStatus.Preparing;
    public DateTime? ShippedAtUtc { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }

    private PhysicalProductPurchase() { }

    public static PhysicalProductPurchase Create(
        PhysicalProductPurchaseId id,
        Guid cartId,
        UserId buyerId,
        string externalProductId,
        string productTitle,
        int quantity,
        decimal unitPriceInNoor,
        decimal paidAmountInNoor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalProductId);
        ArgumentException.ThrowIfNullOrWhiteSpace(productTitle);
        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }
        if (unitPriceInNoor < 0 || decimal.Truncate(unitPriceInNoor) != unitPriceInNoor)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPriceInNoor));
        }
        if (paidAmountInNoor < 0 || decimal.Truncate(paidAmountInNoor) != paidAmountInNoor)
        {
            throw new ArgumentOutOfRangeException(nameof(paidAmountInNoor));
        }

        return new PhysicalProductPurchase
        {
            Id = id,
            CartId = cartId,
            BuyerId = buyerId,
            ExternalProductId = externalProductId.Trim(),
            ProductTitle = productTitle.Trim(),
            Quantity = quantity,
            UnitPriceInNoor = unitPriceInNoor,
            PaidAmountInNoor = paidAmountInNoor
        };
    }

    public void RecordPaymentShares(PaymentShares shares)
    {
        shares.EnsureMatches(PaidAmountInNoor);
        RegularAmountInNoor = shares.RegularInNoor;
        GiftAmountInNoor = shares.GiftInNoor;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFree(Guid? referenceId = null)
    {
        if (PaidAmountInNoor > 0)
        {
            throw new DomainException(DomainErrors.PhysicalProductPurchase_CannotMarkNonFreeAsFree);
        }

        if (referenceId.HasValue && referenceId.Value == Guid.Empty)
        {
            throw new DomainException(DomainErrors.CoursePurchase_InvalidLedgerTransactionId);
        }

        LedgerTransactionId = referenceId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDebited(Guid ledgerTransactionId)
    {
        LedgerTransactionId = ledgerTransactionId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkShipped()
    {
        if (DeliveryStatus == PhysicalDeliveryStatus.Shipped)
        {
            return;
        }

        if (DeliveryStatus != PhysicalDeliveryStatus.Preparing)
        {
            throw InvalidDeliveryTransition(PhysicalDeliveryStatus.Shipped);
        }

        var now = DateTime.UtcNow;
        DeliveryStatus = PhysicalDeliveryStatus.Shipped;
        ShippedAtUtc = now;
        UpdatedAt = now;
    }

    public void MarkDelivered()
    {
        if (DeliveryStatus == PhysicalDeliveryStatus.Delivered)
        {
            return;
        }

        if (DeliveryStatus != PhysicalDeliveryStatus.Shipped)
        {
            throw InvalidDeliveryTransition(PhysicalDeliveryStatus.Delivered);
        }

        var now = DateTime.UtcNow;
        DeliveryStatus = PhysicalDeliveryStatus.Delivered;
        DeliveredAtUtc = now;
        UpdatedAt = now;
    }

    private DomainException InvalidDeliveryTransition(PhysicalDeliveryStatus target) =>
        new(string.Format(DomainErrors.PhysicalProductPurchase_InvalidDeliveryTransition, DeliveryStatus, target));
}
