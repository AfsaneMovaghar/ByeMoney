using ByeMoney.Application.Modules.Wallet.Withdrawals.DTOs;
using ByeMoney.Application.Modules.Wallet.Withdrawals.Interfaces;
using ByeMoney.Application.Modules.Wallet.Withdrawals.Services;
using ByeMoney.Domain.Modules.Wallet.Withdrawals;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetNoorWithdrawals;

public sealed class GetNoorWithdrawalsQueryHandler(INoorWithdrawalStore store) :
    IRequestHandler<GetNoorWithdrawalsQuery, IReadOnlyList<NoorWithdrawalResponse>>
{
    public Task<IReadOnlyList<NoorWithdrawalResponse>> Handle(GetNoorWithdrawalsQuery request, CancellationToken ct)
    {
        NoorWithdrawalStatus? status = NoorWithdrawalRules.TryParseStatus(request.Status, out var parsed)
            ? parsed
            : null;
        var userExternalId = string.IsNullOrWhiteSpace(request.UserExternalId)
            ? null
            : request.UserExternalId.Trim();

        return store.ListAsync(status, userExternalId, request.Limit, ct);
    }
}
