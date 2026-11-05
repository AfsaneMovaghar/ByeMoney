using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

/// <summary>
/// <c>Quantity</c> همان تعداد خرید است و با لغو تغییر نمی‌کند؛
/// <c>CancelledQuantity</c> تعداد لغوشدهٔ بخش کالای فیزیکی است (برای اقلام دوره همیشه 0).
/// قلم وقتی کاملاً لغو شده که <c>CancelledQuantity &gt;= Quantity</c>.
/// <c>RefundedRegularInNoor</c> و <c>RefundedGiftInNoor</c> جمع نور برگشتی (عادی و هدیه) این قلم‌اند؛ برای اقلام دوره همیشه 0.
/// </summary>
public sealed record CourseOrderItem(
    string ExternalCourseId, string? Title, decimal? PriceInNoor, decimal? FinalAmountInNoor,
    Guid? PurchaseId, Guid? TransactionId, DateTime? PurchasedAtUtc,
    string FinancialStatus, string DeliveryStatus,
    string ItemType = nameof(ProductItemType.Course), string? ParentExternalId = null,
    int Quantity = 1, string? PhysicalDeliveryStatus = null,
    int CancelledQuantity = 0,
    decimal RefundedRegularInNoor = 0m, decimal RefundedGiftInNoor = 0m);
