using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.History.DTOs;

public sealed record PurchaseReadRow(
    Guid PurchaseId, Guid? CartId, string CourseTitle, decimal FinalAmountInNoor,
    DateTime PurchasedAtUtc, CoursePurchaseStatus Status,
    string BuyerExternalUserId, string? BuyerFirstName, string? BuyerLastName,
    ProductItemType ItemType = ProductItemType.Course, string? ParentExternalId = null);
