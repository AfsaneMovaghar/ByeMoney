using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.ReopenGatewayReview;

public sealed record ReopenGatewayReviewCommand(string ClientReferenceCode, string CaseId,
    string EvidenceId, string? Note) : IRequest<Result<GatewayReviewState>>;
