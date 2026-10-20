namespace ByeMoney.API.Contracts.TopUp;

public sealed record OpenGatewayReviewRequest(string ClientReferenceCode, string CaseId, string ReasonCode);
