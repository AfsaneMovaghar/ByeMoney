using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.History.DTOs;

public sealed record PurchaseListItem(
    Guid PurchaseId, Guid? CartId, string ItemTitle, decimal FinalAmountInNoor,
    DateTime PurchasedAtUtc, string FinancialStatus, string DeliveryStatus,
    string ItemType = nameof(ProductItemType.Course), string? ParentExternalId = null,
    string? ExternalItemId = null, int Quantity = 1, int CancelledQuantity = 0,
    string? PhysicalDeliveryStatus = null);
