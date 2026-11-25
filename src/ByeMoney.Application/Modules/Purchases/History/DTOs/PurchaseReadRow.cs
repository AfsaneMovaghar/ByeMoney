using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.History.DTOs;

public sealed record PurchaseReadRow(
    Guid PurchaseId, Guid? CartId, string ItemTitle, decimal FinalAmountInNoor,
    DateTime PurchasedAtUtc, CoursePurchaseStatus Status,
    string BuyerExternalUserId, string? BuyerFirstName, string? BuyerLastName,
    ProductItemType ItemType = ProductItemType.Course, string? ParentExternalId = null,
    PhysicalDeliveryStatus? PhysicalDelivery = null,
    string? ExternalItemId = null, int Quantity = 1, int CancelledQuantity = 0);
