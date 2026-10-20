using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Domain.Common.Generic;
using MediatR;
using ByeMoney.Domain.Modules.Wallet.TopUps;

namespace ByeMoney.Application.Modules.Wallet.Commands.ResolveGatewayReview;

public sealed record ResolveGatewayReviewCommand(string ClientReferenceCode, string CaseId,
    string OutcomeCode, string? ResolutionFinancialReferenceId, FinancialReviewEvidence? Evidence = null)
    : IRequest<Result<GatewayReviewState>>;
