using ByeMoney.Application.Modules.Wallet.Transactions.DTOs;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetWalletTransactions;

public sealed record GetWalletTransactionsQuery(int Page = 1, int PageSize = 20)
    : IRequest<WalletTransactionPage<WalletTransactionItem>>;
