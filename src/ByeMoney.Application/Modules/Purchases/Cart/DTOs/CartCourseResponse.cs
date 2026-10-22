using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Cart.DTOs;

public sealed record CartCourseResponse(
    string ExternalCourseId,
    string Title,
    decimal PriceInNoor,
    string PurchaseStatus,
    string ItemType = nameof(ProductItemType.Course),
    string? ParentExternalId = null,
    int Quantity = 1,
    decimal? LineTotalInNoor = null);

