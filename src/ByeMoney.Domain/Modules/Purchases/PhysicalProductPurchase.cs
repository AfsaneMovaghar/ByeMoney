using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Users;

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
    public Guid? LedgerTransactionId { get; private set; }

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

    public void MarkDebited(Guid ledgerTransactionId)
    {
        LedgerTransactionId = ledgerTransactionId;
        UpdatedAt = DateTime.UtcNow;
    }
}
