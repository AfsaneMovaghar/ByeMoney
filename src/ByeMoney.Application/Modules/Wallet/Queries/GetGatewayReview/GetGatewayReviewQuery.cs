using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetGatewayReview;

public sealed record GetGatewayReviewQuery(string ClientReferenceCode) : IRequest<GatewayReviewDetails?>;
