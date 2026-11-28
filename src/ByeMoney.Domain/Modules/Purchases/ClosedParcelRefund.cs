using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet;

namespace ByeMoney.Domain.Modules.Purchases;

/// <summary>
/// سابقهٔ برگشت دستی نور برای قلم کالای فیزیکی بسته‌شده بدون تسویه (ClosedWithoutSettlement).
/// این جدول از PhysicalProductCancellations جداست تا صف برگشت موجودی استرپی و CancelledQuantity دست نخورند.
/// </summary>
public class ClosedParcelRefund : BaseEntity<ClosedParcelRefundId>
{
    public const int ReasonMaxLength = 500;

    public Guid OperationId { get; private set; }
    public Guid CartId { get; private set; }
    public PhysicalProductPurchaseId PurchaseId { get; private set; }
    public string ScopeId { get; private set; } = FinancialScope.TarheElahi;
    public decimal RegularRefundInNoor { get; private set; }
    public decimal GiftRefundInNoor { get; private set; }
    public UserId PerformedByUserId { get; private set; }
    public string Reason { get; private set; } = null!;

    private ClosedParcelRefund() { }

    public static ClosedParcelRefund Create(
        Guid operationId,
        PhysicalProductPurchase purchase,
        PaymentShares refund,
        UserId performedByUserId,
        string reason,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(purchase);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (operationId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(operationId));
        }

        if (refund.RegularInNoor < 0 || refund.GiftInNoor < 0 || refund.TotalInNoor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(refund));
        }

        var normalizedReason = reason.Trim();
        if (normalizedReason.Length > ReasonMaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        var microsecondTimestamp = utcNow.AddTicks(-(utcNow.Ticks % TimeSpan.TicksPerMicrosecond));

        return new ClosedParcelRefund
        {
            Id = ClosedParcelRefundId.New(),
            CreatedAtUtc = microsecondTimestamp,
            OperationId = operationId,
            CartId = purchase.CartId,
            PurchaseId = purchase.Id,
            ScopeId = FinancialScope.Require(purchase.ScopeId),
            RegularRefundInNoor = refund.RegularInNoor,
            GiftRefundInNoor = refund.GiftInNoor,
            PerformedByUserId = performedByUserId,
            Reason = normalizedReason
        };
    }
}
