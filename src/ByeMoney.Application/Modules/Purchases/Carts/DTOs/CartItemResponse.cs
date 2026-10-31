using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Carts.DTOs;

public sealed record CartItemResponse(
    string ExternalCourseId,
    string Title,
    decimal PriceInNoor,
    string PurchaseStatus,
    string ItemType = nameof(ProductItemType.Course),
    string? ParentExternalId = null,
    int Quantity = 1,
    decimal? LineTotalInNoor = null);

