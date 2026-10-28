using System.Text.Json.Serialization;
using ByeMoney.Application.Modules.Wallet.Commands.AdminGrantGiftNoor;

namespace ByeMoney.API.Contracts.Gift;

public sealed record AdminGiftErrorResponse(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("errors")] IReadOnlyList<object> Errors,
    [property: JsonPropertyName("invalidUsers")] IReadOnlyList<InvalidGiftUserDto>? InvalidUsers = null);

