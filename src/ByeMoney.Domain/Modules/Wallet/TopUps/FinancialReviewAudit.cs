namespace ByeMoney.Domain.Modules.Wallet.TopUps;

public sealed record FinancialReviewEvidence(Guid OperationId, int ExpectedRevision,
    DateOnly CheckedReportDate, bool? MatchingDepositFound, string? Note,
    string? DepositReference, DateOnly? DepositDate, decimal? DepositAmountRial,
    string? ManualRefundReference);

public sealed record FinancialReviewAudit(string EventId, string EventType, int Revision,
    DateTime OccurredAtUtc, Guid? ActorUserId, string? ActorName, string? OutcomeCode,
    string? FinancialReferenceId, FinancialReviewEvidence? Evidence, string? Note);
