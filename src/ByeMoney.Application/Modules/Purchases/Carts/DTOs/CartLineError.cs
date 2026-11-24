using System.Text.Json.Serialization;

namespace ByeMoney.Application.Modules.Purchases.Carts.DTOs;

/// <summary>
/// قلمی از سبد که موجودی کافی ندارد (I09-PH). مقدار <see cref="Reason"/> یکی از <c>CartStockReasons</c> است.
/// </summary>
public sealed record CartLineError(
    string ExternalId,
    string Reason,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? AvailableQuantity = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Type = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Quantity = null);
