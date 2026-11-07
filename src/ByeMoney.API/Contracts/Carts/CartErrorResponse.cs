using System.Text.Json.Serialization;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;

namespace ByeMoney.API.Contracts.Carts;

public sealed record CartErrorResponse(
    string Code,
    string Error,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    CartLineError? LineError = null);
