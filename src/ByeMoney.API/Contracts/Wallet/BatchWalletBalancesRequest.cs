namespace ByeMoney.API.Contracts.Wallet;

public sealed record BatchWalletBalancesRequest(IReadOnlyList<string> UserIds);
