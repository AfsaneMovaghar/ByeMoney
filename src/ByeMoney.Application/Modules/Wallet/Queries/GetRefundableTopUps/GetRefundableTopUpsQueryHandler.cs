using ByeMoney.Application.Modules.Wallet.Withdrawals.DTOs;
using ByeMoney.Application.Modules.Wallet.Withdrawals.Interfaces;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetRefundableTopUps;

public sealed class GetRefundableTopUpsQueryHandler(INoorWithdrawalStore store) :
    IRequestHandler<GetRefundableTopUpsQuery, IReadOnlyList<RefundableTopUpResponse>>
{
    public Task<IReadOnlyList<RefundableTopUpResponse>> Handle(GetRefundableTopUpsQuery request, CancellationToken ct) =>
        store.ListRefundableTopUpsAsync(request.UserExternalId.Trim(), GetRefundableTopUpsQuery.MaxResults, ct);
}
