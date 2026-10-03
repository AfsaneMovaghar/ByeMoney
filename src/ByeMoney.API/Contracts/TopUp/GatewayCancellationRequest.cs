namespace ByeMoney.API.Contracts.TopUp;

public sealed record GatewayCancellationRequest(string ClientReferenceCode, string Gateway);
