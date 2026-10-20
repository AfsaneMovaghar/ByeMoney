namespace ByeMoney.API.Contracts.TopUp;

public sealed record ReopenGatewayReviewRequest(string ClientReferenceCode, string CaseId, string EvidenceId, string? Note);
