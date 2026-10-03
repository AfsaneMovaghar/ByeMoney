using System.Text.Json.Serialization;

namespace ByeMoney.API.Contracts.TopUp;

public sealed record GatewayConfirmationResponse(
    string Status,
    string ClientReferenceCode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Idempotent = null);
