using ByeMoney.Application.Modules.Wallet.Commands.CancelGatewayTopUp;
using ByeMoney.Application.Modules.Wallet.Commands.ConfirmGatewayTopUp;
using ByeMoney.Application.Modules.Wallet.Commands.RecordGatewayResult;
using ByeMoney.Domain.Common;

namespace ByeMoney.Application.Modules.Wallet.Interfaces;

public interface IGatewayTopUpService
{
    Task<Result<GatewayReviewState>> OpenReviewAsync(string clientReferenceCode, string caseId, string reasonCode, CancellationToken ct = default);
    Task<Result<GatewayReviewState>> ResolveReviewAsync(string clientReferenceCode, string caseId, string outcomeCode, string? financialReferenceId, CancellationToken ct = default);
    Task<GatewayConfirmationOutcome> ConfirmAsync(ConfirmGatewayTopUpCommand command, CancellationToken ct = default);
    Task<Result> CancelAsync(CancelGatewayTopUpCommand command, CancellationToken ct = default);
    Task<Result> RecordResultAsync(RecordGatewayResultCommand command, CancellationToken ct = default);
}

public sealed record GatewayReviewState(string ClientReferenceCode, string CaseId, string TopUpStatus,
    string ReasonCode, DateTime OpenedAtUtc, DateTime? ResolvedAtUtc, string? OutcomeCode,
    string? ResolutionFinancialReferenceId);
