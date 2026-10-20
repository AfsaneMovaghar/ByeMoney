using ByeMoney.Domain.Modules.Wallet.TopUps;

namespace ByeMoney.Application.Modules.Wallet.DTOs;

public sealed record GatewayReviewState(string ClientReferenceCode, string CaseId, string TopUpStatus,
    string ReasonCode, DateTime OpenedAtUtc, DateTime? ResolvedAtUtc, string? OutcomeCode,
    string? ResolutionFinancialReferenceId, int Revision = 0,
    IReadOnlyList<FinancialReviewAudit>? Audit = null, string? ManualRefundReference = null);
