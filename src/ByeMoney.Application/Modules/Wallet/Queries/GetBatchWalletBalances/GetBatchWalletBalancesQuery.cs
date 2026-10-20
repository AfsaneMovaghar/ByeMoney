using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetBatchWalletBalances;

public sealed record GetBatchWalletBalancesQuery(IReadOnlyList<string> UserIds)
    : IRequest<BatchWalletBalancesResponse>;
