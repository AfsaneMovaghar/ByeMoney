using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Wallet;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetGatewayTopUpDetails;

public sealed class GetGatewayTopUpDetailsQueryHandler(ITopUpRequestRepository topUps)
    : IRequestHandler<GetGatewayTopUpDetailsQuery, GatewayTopUpDetailsDto?>
{
    public async Task<GatewayTopUpDetailsDto?> Handle(GetGatewayTopUpDetailsQuery request, CancellationToken ct)
    {
        var topUp = await topUps.GetByClientReferenceCodeAsync(request.ClientReferenceCode, ct);
        if (topUp is null || topUp.ScopeId != FinancialScope.TarheElahi)
            return null;
        return new GatewayTopUpDetailsDto(
            topUp.Id.Value, topUp.ClientReferenceCode, topUp.AmountRial,
            topUp.Status.ToString(), topUp.PaymentMethod.ToString(),
            topUp.ExternalTransactionId, topUp.BankReferenceNumber, topUp.GatewayName, topUp.ManualRefundReference is not null);
    }
}
