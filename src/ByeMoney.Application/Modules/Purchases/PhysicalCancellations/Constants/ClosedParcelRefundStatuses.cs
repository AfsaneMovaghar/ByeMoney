namespace ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Constants;

/// <summary>وضعیت نمایشی برگشت دستی نور برای قلم بسته‌شده بدون تسویه.</summary>
public static class ClosedParcelRefundStatuses
{
    public const string None = "None";
    public const string NotRefunded = "NotRefunded";
    public const string Refunded = "Refunded";
    public const string ManualReview = "ManualReview";
}
