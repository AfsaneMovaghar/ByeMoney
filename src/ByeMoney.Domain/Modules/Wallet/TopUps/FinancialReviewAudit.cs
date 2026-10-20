

namespace ByeMoney.Domain.Modules.Wallet.TopUps;

public sealed record FinancialReviewAudit(string EventId, string EventType, int Revision,
    DateTime OccurredAtUtc, Guid? ActorUserId, string? ActorName, string? OutcomeCode,
    string? FinancialReferenceId, FinancialReviewEvidence? Evidence, string? Note);
