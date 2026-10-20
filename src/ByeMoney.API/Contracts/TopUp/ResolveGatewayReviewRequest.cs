using ByeMoney.Domain.Modules.Wallet.TopUps;

namespace ByeMoney.API.Contracts.TopUp;

public sealed record ResolveGatewayReviewRequest(string ClientReferenceCode, string CaseId,
    string OutcomeCode, string? ResolutionFinancialReferenceId, FinancialReviewEvidence? Evidence = null);
