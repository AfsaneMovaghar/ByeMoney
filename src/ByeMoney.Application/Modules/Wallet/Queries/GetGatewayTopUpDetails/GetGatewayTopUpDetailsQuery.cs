using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetGatewayTopUpDetails;

public sealed record GatewayTopUpDetailsDto(
    Guid TopUpRequestId,
    string ClientReferenceCode,
    decimal? AmountRial,
    string Status,
    string PaymentMethod,
    string? ExternalTransactionId,
    string? BankReferenceNumber,
    string? GatewayName);

public sealed record GetGatewayTopUpDetailsQuery(string ClientReferenceCode) : IRequest<GatewayTopUpDetailsDto?>;
