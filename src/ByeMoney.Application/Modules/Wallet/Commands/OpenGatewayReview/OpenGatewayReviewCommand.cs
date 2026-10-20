using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.OpenGatewayReview;

public sealed record OpenGatewayReviewCommand(string ClientReferenceCode, string CaseId, string ReasonCode)
    : IRequest<Result<GatewayReviewState>>;
