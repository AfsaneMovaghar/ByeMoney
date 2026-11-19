namespace ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Constants;

/// <summary>کدهای هشدار پیش‌نمایش لغو (D09).</summary>
public static class PhysicalCancellationWarningCodes
{
    public const string CouponRemoved = "COUPON_REMOVED";
    public const string FreeShippingRemoved = "FREE_SHIPPING_REMOVED";
    public const string ShippingNotRefunded = "SHIPPING_NOT_REFUNDED";

    /// <summary>مرسولهٔ پرداخت‌نشده در واسط باقی می‌ماند و پاک‌سازی آن دستی است.</summary>
    public const string CarrierPaymentPending = "CARRIER_PAYMENT_PENDING";
}
