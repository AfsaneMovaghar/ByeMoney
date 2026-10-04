using ByeMoney.Application.Modules.Wallet.Commands.CancelGatewayTopUp;
using ByeMoney.Application.Modules.Wallet.Commands.ConfirmGatewayTopUp;
using ByeMoney.Application.Modules.Wallet.Commands.RecordGatewayResult;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Wallet.TopUps;

namespace ByeMoney.Application.Modules.Wallet.Interfaces;

public interface IGatewayTopUpService
{
    Task<Result<GatewayReviewState>> OpenReviewAsync(string clientReferenceCode, string caseId, string reasonCode, CancellationToken ct = default);
    Task<Result<GatewayReviewState>> ResolveReviewAsync(string clientReferenceCode, string caseId, string outcomeCode, string? financialReferenceId, CancellationToken ct = default);
    Task<Result<GatewayReviewState>> ResolveManualReviewAsync(string clientReferenceCode, string caseId,
        string outcomeCode, string? financialReferenceId, FinancialReviewEvidence evidence,
        Guid actorUserId, string? actorName, CancellationToken ct = default);
    Task<Result<GatewayReviewState>> ReopenReviewAsync(string clientReferenceCode, string caseId,
        string evidenceId, string? note, CancellationToken ct = default);
    Task<GatewayConfirmationOutcome> ConfirmAsync(ConfirmGatewayTopUpCommand command, CancellationToken ct = default);
    Task<Result> CancelAsync(CancelGatewayTopUpCommand command, CancellationToken ct = default);
    Task<Result> RecordResultAsync(RecordGatewayResultCommand command, CancellationToken ct = default);
}

public sealed record GatewayReviewState(string ClientReferenceCode, string CaseId, string TopUpStatus,
    string ReasonCode, DateTime OpenedAtUtc, DateTime? ResolvedAtUtc, string? OutcomeCode,
    string? ResolutionFinancialReferenceId, int Revision = 0,
    IReadOnlyList<FinancialReviewAudit>? Audit = null, string? ManualRefundReference = null);
