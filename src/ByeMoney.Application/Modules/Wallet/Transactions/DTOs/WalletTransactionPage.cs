namespace ByeMoney.Application.Modules.Wallet.Transactions.DTOs;

public sealed record WalletTransactionPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
