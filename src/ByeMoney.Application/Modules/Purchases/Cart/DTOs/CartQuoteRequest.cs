using System.Text.Json.Serialization;

namespace ByeMoney.Application.Modules.Purchases.Cart.DTOs;

public sealed record CartQuoteRequest(
    Guid CartId,
    string BuyerExternalUserId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string[]? ExternalCourseIds,
    string? CouponCode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<CartQuoteItemRequest>? Items = null);

public sealed record CartQuoteItemRequest(
    string ExternalId,
    string Type,
    int Quantity);

