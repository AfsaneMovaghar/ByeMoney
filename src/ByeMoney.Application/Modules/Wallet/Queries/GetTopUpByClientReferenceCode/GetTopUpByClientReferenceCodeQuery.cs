using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetTopUpByClientReferenceCode;

public record GetTopUpByClientReferenceCodeQuery(string ClientReferenceCode) : IRequest<TopUpRequestDto?>;
