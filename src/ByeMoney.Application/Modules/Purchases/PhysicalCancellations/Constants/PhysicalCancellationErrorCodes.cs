namespace ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Constants;

/// <summary>کدهای خطای ساختاریافتهٔ لغو کالای فیزیکی؛ HTTP آن‌ها در کنترلر نگاشت می‌شود.</summary>
public static class PhysicalCancellationErrorCodes
{
    public const string NotFound = "PHYSICAL_CANCEL_NOT_FOUND";
    public const string Conflict = "PHYSICAL_CANCEL_CONFLICT";
    public const string AlreadyShipped = "PHYSICAL_CANCEL_ALREADY_SHIPPED";
    public const string ManualReview = "PHYSICAL_CANCEL_MANUAL_REVIEW";
    public const string Unavailable = "PHYSICAL_CANCEL_UNAVAILABLE";

    /// <summary>
    /// نشانی که پس از بستن دستی برگشت موجودی در <c>StockReturnErrorCode</c> می‌ماند،
    /// تا بتوان بستن دستی را از تأیید خودکار استرپی تشخیص داد.
    /// </summary>
    public const string RestockResolvedManually = "RESTOCK_RESOLVED_MANUALLY";
}

