using ByeMoney.Application.Modules.Wallet.Interfaces;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetGatewayTopUpDetails;

public sealed class GetGatewayTopUpDetailsQueryHandler(ITopUpRequestRepository topUps)
    : IRequestHandler<GetGatewayTopUpDetailsQuery, GatewayTopUpDetailsDto?>
{
    public async Task<GatewayTopUpDetailsDto?> Handle(GetGatewayTopUpDetailsQuery request, CancellationToken ct)
    {
        var topUp = await topUps.GetByClientReferenceCodeAsync(request.ClientReferenceCode, ct);
        return topUp is null ? null : new GatewayTopUpDetailsDto(
            topUp.Id.Value, topUp.ClientReferenceCode, topUp.AmountRial,
            topUp.Status.ToString(), topUp.PaymentMethod.ToString(),
            topUp.ExternalTransactionId, topUp.BankReferenceNumber, topUp.GatewayName, topUp.ManualRefundReference is not null);
    }
}
