using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetGatewayTopUpDetails;

public sealed record GetGatewayTopUpDetailsQuery(string ClientReferenceCode) : IRequest<GatewayTopUpDetailsDto?>;
