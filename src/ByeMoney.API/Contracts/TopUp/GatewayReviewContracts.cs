namespace ByeMoney.API.Contracts.TopUp;

public sealed record OpenGatewayReviewRequest(string ClientReferenceCode, string CaseId, string ReasonCode);

public sealed record ResolveGatewayReviewRequest(string ClientReferenceCode, string CaseId,
    string OutcomeCode, string? ResolutionFinancialReferenceId);

public sealed record GatewayReviewResponse(string ClientReferenceCode, string CaseId, string TopUpStatus,
    string ReasonCode, DateTime OpenedAtUtc, DateTime? ResolvedAtUtc, string? OutcomeCode,
    string? ResolutionFinancialReferenceId);
