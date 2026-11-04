namespace ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;

/// <summary>نتیجهٔ لغو؛ <c>Outcome</c> رشتهٔ <see cref="PhysicalCancellationOutcome"/> و <c>Actor</c> رشتهٔ اجراکننده است.</summary>
public sealed record PhysicalCancellationResponse(
    Guid OperationId,
    Guid CartId,
    string Outcome,
    string Actor,
    IReadOnlyList<PhysicalCancellationItemResponse> Items,
    decimal ShippingRegularRefundInNoor,
    decimal ShippingGiftRefundInNoor,
    decimal TotalRefundInNoor,
    DateTime? CancelledAtUtc);

public sealed record PhysicalCancellationItemResponse(
    Guid PurchaseId,
    string ExternalProductId,
    string Title,
    int Quantity,
    decimal RegularRefundInNoor,
    decimal GiftRefundInNoor);

/// <summary>پیش‌نمایش بدون اثر لغو، همراه هشدار حذف کوپن و ارسال رایگان.</summary>
public sealed record PhysicalCancellationPreviewResponse(
    Guid CartId,
    string Outcome,
    IReadOnlyList<PhysicalCancellationItemResponse> Items,
    decimal ShippingRegularRefundInNoor,
    decimal ShippingGiftRefundInNoor,
    decimal TotalRefundInNoor,
    IReadOnlyList<PhysicalCancellationWarning> Warnings);

public sealed record PhysicalCancellationWarning(string Code, string Message);

public enum PhysicalCancellationOutcome
{
    /// <summary>خرید پرداخت‌شده لغو و نور برگشت داده شد.</summary>
    Refunded = 1,

    /// <summary>سبد پرداخت‌نشده بود؛ فقط رزرو طبق I09-C آزاد شد و برگشتی وجود ندارد.</summary>
    ReservationReleased = 2
}
