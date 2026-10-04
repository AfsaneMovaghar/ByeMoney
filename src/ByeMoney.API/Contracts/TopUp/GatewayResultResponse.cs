namespace ByeMoney.API.Contracts.TopUp;

public sealed record GatewayResultResponse(string ClientReferenceCode, string Status);
