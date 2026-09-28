using ByeMoney.Application.Modules.Wallet.Interfaces;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetBatchWalletBalances;

public sealed class GetBatchWalletBalancesQueryHandler(IWalletRepository wallets)
    : IRequestHandler<GetBatchWalletBalancesQuery, BatchWalletBalancesResponse>
{
    public async Task<BatchWalletBalancesResponse> Handle(GetBatchWalletBalancesQuery request, CancellationToken ct)
    {
        var userIds = request.UserIds.Distinct(StringComparer.Ordinal).ToArray();
        var found = await wallets.GetBalancesByExternalUserIdsAsync(userIds, ct);
        var balances = userIds.ToDictionary(id => id, id => found.GetValueOrDefault(id), StringComparer.Ordinal);
        return new BatchWalletBalancesResponse(true, balances);
    }
}
