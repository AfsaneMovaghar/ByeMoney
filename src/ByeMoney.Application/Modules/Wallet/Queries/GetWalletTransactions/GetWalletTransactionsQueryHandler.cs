using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Constants;
using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Modules.Wallet.Transactions.DTOs;
using ByeMoney.Application.Modules.Wallet.Transactions.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetWalletTransactions;

public sealed class GetWalletTransactionsQueryHandler(
    IWalletTransactionReader reader,
    ICurrentUserService currentUser) :
    IRequestHandler<GetWalletTransactionsQuery, WalletTransactionPage<WalletTransactionItem>>
{
    public async Task<WalletTransactionPage<WalletTransactionItem>> Handle(
        GetWalletTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

        var page = await reader.ListAsync(
            new UserId(userId), FinancialScope.TarheElahi, request.Page, request.PageSize, cancellationToken);

        var items = page.Items.Select(ToItem).ToArray();
        return new WalletTransactionPage<WalletTransactionItem>(items, page.Page, page.PageSize, page.TotalCount);
    }

    private static WalletTransactionItem ToItem(WalletTransactionRow row)
    {
        var orderId = row.OrderCartId.HasValue
            ? OrderIdFormat.For(OrderSourceType.Cart, row.OrderCartId.Value)
            : null;

        var topUpId = row.ReferenceType == LedgerReferenceType.TopUp
            ? row.ReferenceId
            : null;

        return new WalletTransactionItem(
            row.TransactionId,
            row.ReferenceType.ToString(),
            row.AmountNoor,
            row.GiftAmountNoor,
            row.CreatedAtUtc,
            orderId,
            topUpId);
    }
}
