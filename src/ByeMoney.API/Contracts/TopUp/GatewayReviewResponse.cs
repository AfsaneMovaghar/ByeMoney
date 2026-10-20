namespace ByeMoney.API.Contracts.TopUp;

public sealed record GatewayReviewResponse(string ClientReferenceCode, string CaseId, string TopUpStatus,
    string ReasonCode, DateTime OpenedAtUtc, DateTime? ResolvedAtUtc, string? OutcomeCode,
    string? ResolutionFinancialReferenceId);
