

namespace ByeMoney.Domain.Modules.Wallet.TopUps;

public sealed record FinancialReviewEvidence(Guid OperationId, int ExpectedRevision,
    DateOnly CheckedReportDate, bool? MatchingDepositFound, string? Note,
    string? DepositReference, DateOnly? DepositDate, decimal? DepositAmountRial,
    string? ManualRefundReference);
