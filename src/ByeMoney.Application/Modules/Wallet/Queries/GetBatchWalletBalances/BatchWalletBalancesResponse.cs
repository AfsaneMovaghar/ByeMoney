namespace ByeMoney.Application.Modules.Wallet.Queries.GetBatchWalletBalances;

public sealed record BatchWalletBalancesResponse(bool Success, Dictionary<string, decimal> Balances);
