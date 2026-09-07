using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetTopUpByClientReferenceCode;

public class GetTopUpByClientReferenceCodeQueryHandler
    : IRequestHandler<GetTopUpByClientReferenceCodeQuery, TopUpRequestDto?>
{
    private readonly ITopUpRequestRepository _topUpRequestRepository;

    public GetTopUpByClientReferenceCodeQueryHandler(ITopUpRequestRepository topUpRequestRepository)
    {
        _topUpRequestRepository = topUpRequestRepository;
    }

    public async Task<TopUpRequestDto?> Handle(
        GetTopUpByClientReferenceCodeQuery request, CancellationToken cancellationToken)
    {
        var topUp = await _topUpRequestRepository
            .GetByClientReferenceCodeAsync(request.ClientReferenceCode, cancellationToken);

        return topUp is null ? null : MapToDto(topUp);
    }

    private static TopUpRequestDto MapToDto(TopUpRequest t) => new(
        t.Id.Value, t.UserId.Value, t.Amount,
        t.PaymentMethod.ToString(), t.ClientReferenceCode,
        t.Status.ToString(), t.ExternalTransactionId,
        t.RejectionReason, t.CreatedAtUtc,
        t.ConfirmedAtUtc, t.RejectedAtUtc);
}
