using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Users;

namespace ByeMoney.Domain.Modules.Purchases;

/// <summary>
/// سابقهٔ یک قلم در یک عملیات لغو کالای فیزیکی. شناسهٔ عملیات و شناسهٔ خرید با هم یکتا هستند
/// تا تکرار یا هم‌زمانی همان عملیات برگشت مضاعف نسازد.
/// </summary>
public class PhysicalProductCancellation : BaseEntity<PhysicalProductCancellationId>
{
    public Guid OperationId { get; private set; }
    public Guid CartId { get; private set; }
    public PhysicalProductPurchaseId PurchaseId { get; private set; }
    public int Quantity { get; private set; }
    public decimal RegularRefundInNoor { get; private set; }
    public decimal GiftRefundInNoor { get; private set; }

    /// <summary>سهم عادی برگشت ارسال؛ فقط در ردیفی که لغو کامل مرسوله را تمام کرده، غیرصفر است.</summary>
    public decimal ShippingRegularRefundInNoor { get; private set; }

    /// <summary>سهم هدیهٔ برگشت ارسال؛ فقط در ردیفی که لغو کامل مرسوله را تمام کرده، غیرصفر است.</summary>
    public decimal ShippingGiftRefundInNoor { get; private set; }

    public PhysicalCancellationActor Actor { get; private set; }
    public UserId PerformedByUserId { get; private set; }
    public string Reason { get; private set; } = null!;

    /// <summary>
    /// زمان تأیید برگشت موجودی در استرپی. خالی یعنی برگشت موجودی این عملیات هنوز در صف تلاش مجدد است؛
    /// برگشت نور مستقل از آن و پیش‌تر commit شده است.
    /// </summary>
    public DateTime? StockReturnedAtUtc { get; private set; }

    /// <summary>کد خطای آخرین تلاش ناموفق برگشت موجودی، برای رسیدگی عملیاتی.</summary>
    public string? StockReturnErrorCode { get; private set; }

    /// <summary>تعداد تلاش‌های ناموفق برگشت موجودی؛ با رسیدن به سقف تنظیم‌شده، بازیابی پس‌زمینه عملیات را رها می‌کند.</summary>
    public int StockReturnAttempts { get; private set; }

    private PhysicalProductCancellation() { }

    public static PhysicalProductCancellation Create(
        Guid operationId,
        PhysicalProductPurchase purchase,
        int quantity,
        PaymentShares refund,
        PaymentShares shippingRefund,
        PhysicalCancellationActor actor,
        UserId performedByUserId,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(purchase);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (operationId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(operationId));
        }

        var now = DateTime.UtcNow;

        return new PhysicalProductCancellation
        {
            Id = PhysicalProductCancellationId.New(),
            // دقت timestamp در PostgreSQL میکروثانیه است؛ همین مقدار ذخیره و برگردانده می‌شود تا پاسخ تکرار عملیات یکسان بماند.
            CreatedAtUtc = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond)),
            OperationId = operationId,
            CartId = purchase.CartId,
            PurchaseId = purchase.Id,
            Quantity = quantity,
            RegularRefundInNoor = refund.RegularInNoor,
            GiftRefundInNoor = refund.GiftInNoor,
            ShippingRegularRefundInNoor = shippingRefund.RegularInNoor,
            ShippingGiftRefundInNoor = shippingRefund.GiftInNoor,
            Actor = actor,
            PerformedByUserId = performedByUserId,
            Reason = reason.Trim()
        };
    }
}
