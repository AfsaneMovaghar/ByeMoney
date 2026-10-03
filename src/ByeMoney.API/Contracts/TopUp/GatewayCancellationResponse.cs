namespace ByeMoney.API.Contracts.TopUp;

public sealed record GatewayCancellationResponse(string Status, string ClientReferenceCode);
