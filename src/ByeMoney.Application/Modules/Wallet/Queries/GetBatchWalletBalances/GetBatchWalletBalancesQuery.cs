using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetBatchWalletBalances;

public sealed record GetBatchWalletBalancesQuery(IReadOnlyList<string> UserIds)
    : IRequest<BatchWalletBalancesResponse>;

public sealed record BatchWalletBalancesResponse(bool Success, Dictionary<string, decimal> Balances);
