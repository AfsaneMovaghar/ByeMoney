using ByeMoney.Application.Modules.Wallet.Interfaces;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetBatchWalletBalances;

public sealed class GetBatchWalletBalancesQueryHandler(IWalletRepository wallets)
    : IRequestHandler<GetBatchWalletBalancesQuery, BatchWalletBalancesResponse>
{
    public async Task<BatchWalletBalancesResponse> Handle(GetBatchWalletBalancesQuery request, CancellationToken ct)
    {
        var userIds = request.UserIds.Distinct(StringComparer.Ordinal).ToArray();
        var (foundBalances, foundGiftBalances) = await wallets.GetBalancesAndGiftBalancesByExternalUserIdsAsync(userIds, ct);
        var balances = userIds.ToDictionary(id => id, id => foundBalances.GetValueOrDefault(id), StringComparer.Ordinal);
        var giftBalances = userIds.ToDictionary(id => id, id => foundGiftBalances.GetValueOrDefault(id), StringComparer.Ordinal);
        return new BatchWalletBalancesResponse(true, balances, giftBalances);
    }
}

