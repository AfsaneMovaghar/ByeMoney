namespace ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Constants;

/// <summary>کدهای خطای ساختاریافتهٔ برگشت دستی نور برای مرسولهٔ بسته‌شده بدون تسویه.</summary>
public static class ClosedParcelRefundErrorCodes
{
    public const string NotFound = "CLOSED_PARCEL_REFUND_NOT_FOUND";
    public const string NotEligible = "CLOSED_PARCEL_REFUND_NOT_ELIGIBLE";
    public const string AlreadyRefunded = "CLOSED_PARCEL_REFUND_ALREADY_REFUNDED";
    public const string NoRemainingAmount = "CLOSED_PARCEL_REFUND_NO_REMAINING_AMOUNT";
    public const string AlreadySettled = "CLOSED_PARCEL_REFUND_ALREADY_SETTLED";
    public const string ManualReview = "CLOSED_PARCEL_REFUND_MANUAL_REVIEW";
    public const string Conflict = "CLOSED_PARCEL_REFUND_CONFLICT";
}
