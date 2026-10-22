using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

public sealed record CourseOrderItem(
    string ExternalCourseId, string? Title, decimal? PriceInNoor, decimal? FinalAmountInNoor,
    Guid? PurchaseId, Guid? TransactionId, DateTime? PurchasedAtUtc,
    string FinancialStatus, string DeliveryStatus,
    string ItemType = nameof(ProductItemType.Course), string? ParentExternalId = null);
