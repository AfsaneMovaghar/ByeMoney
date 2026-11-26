using ByeMoney.Application.Modules.Wallet.Transactions.DTOs;
using ByeMoney.Application.Modules.Wallet.Transactions.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using WalletEntity = ByeMoney.Domain.Modules.Wallet.Wallets.Wallet;

namespace ByeMoney.Infrastructure.Modules.Wallet.Persistence.Ledger;

/// <summary>
/// خواندن گردش کیف پول کاربر از Ledger. فقط ردیف‌های حساب همان کیف پول خوانده می‌شود و ردیف‌های
/// عادی و هدیهٔ یک تراکنش (TransactionId) در یک ردیف جمع می‌شوند.
/// </summary>
public sealed class WalletTransactionReader(ApplicationDbContext db) : IWalletTransactionReader
{
    public async Task<WalletTransactionPage<WalletTransactionRow>> ListAsync(
        UserId userId, string scopeId, int page, int pageSize, CancellationToken ct)
    {
        var wallet = await db.Set<WalletEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == userId && w.ScopeId == scopeId, ct);

        if (wallet is null)
        {
            return new WalletTransactionPage<WalletTransactionRow>([], page, pageSize, 0);
        }

        var accountEntries = db.Set<LedgerEntry>()
            .AsNoTracking()
            .Where(entry => entry.AccountId == wallet.AccountId);

        var transactions = accountEntries
            .GroupBy(entry => new { entry.TransactionId, entry.ReferenceType })
            .Select(group => new
            {
                group.Key.TransactionId,
                group.Key.ReferenceType,
                AmountNoor = group.Sum(entry => entry.Amount),
                GiftAmountNoor = group.Sum(entry => entry.BalanceType == LedgerBalanceType.Gift ? entry.Amount : 0m),
                CreatedAtUtc = group.Min(entry => entry.CreatedAtUtc)
            });

        var totalCount = await transactions.CountAsync(ct);

        var pageRows = await transactions
            .OrderByDescending(row => row.CreatedAtUtc)
            .ThenByDescending(row => row.TransactionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        if (pageRows.Count == 0)
        {
            return new WalletTransactionPage<WalletTransactionRow>([], page, pageSize, totalCount);
        }

        var transactionIds = pageRows.Select(row => row.TransactionId).ToArray();
        var referenceIds = await LoadReferenceIdsAsync(accountEntries, transactionIds, ct);
        var orderCartIds = await LoadOrderCartIdsAsync(transactionIds, ct);

        var topUpIdsByTransaction = pageRows
            .Where(row => row.ReferenceType == LedgerReferenceType.TopUp)
            .Select(row => new
            {
                row.TransactionId,
                Parsed = Guid.TryParse(referenceIds.GetValueOrDefault(row.TransactionId), out var guid)
                    ? new TopUpRequestId(guid)
                    : (TopUpRequestId?)null
            })
            .Where(x => x.Parsed.HasValue)
            .ToDictionary(x => x.TransactionId, x => x.Parsed!.Value);

        var paymentMethods = await LoadTopUpPaymentMethodsAsync(
            topUpIdsByTransaction.Values.Distinct().ToArray(),
            ct);

        var items = pageRows
            .Select(row => new WalletTransactionRow(
                row.TransactionId,
                row.ReferenceType,
                referenceIds.GetValueOrDefault(row.TransactionId),
                row.AmountNoor,
                row.GiftAmountNoor,
                row.CreatedAtUtc,
                orderCartIds.TryGetValue(row.TransactionId, out var cartId) ? cartId : null,
                topUpIdsByTransaction.TryGetValue(row.TransactionId, out var topUpId) &&
                    paymentMethods.TryGetValue(topUpId, out var method)
                    ? method
                    : null))
            .ToArray();

        return new WalletTransactionPage<WalletTransactionRow>(items, page, pageSize, totalCount);
    }

    private static async Task<Dictionary<Guid, string?>> LoadReferenceIdsAsync(
        IQueryable<LedgerEntry> accountEntries, Guid[] transactionIds, CancellationToken ct)
    {
        var references = await accountEntries
            .Where(entry => transactionIds.Contains(entry.TransactionId))
            .Select(entry => new { entry.TransactionId, entry.ReferenceId })
            .ToListAsync(ct);

        return references
            .GroupBy(reference => reference.TransactionId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(reference => reference.ReferenceId)
                    .Where(referenceId => referenceId is not null)
                    .OrderBy(referenceId => referenceId, StringComparer.Ordinal)
                    .FirstOrDefault());
    }

    /// <summary>
    /// سفارش مرتبط: TransactionId خرید سبد همان شناسهٔ سبد است و TransactionId برگشت لغو فیزیکی همان
    /// OperationId لغو است که سبدش در رکورد لغو ثبت شده است.
    /// </summary>
    private async Task<Dictionary<Guid, Guid>> LoadOrderCartIdsAsync(Guid[] transactionIds, CancellationToken ct)
    {
        var cartIds = await db.Set<Cart>()
            .AsNoTracking()
            .Where(cart => transactionIds.Contains(cart.Id))
            .Select(cart => cart.Id)
            .ToListAsync(ct);

        var cancellations = await db.Set<PhysicalProductCancellation>()
            .AsNoTracking()
            .Where(cancellation => transactionIds.Contains(cancellation.OperationId))
            .Select(cancellation => new { cancellation.OperationId, cancellation.CartId })
            .Distinct()
            .ToListAsync(ct);

        var result = cartIds.ToDictionary(cartId => cartId, cartId => cartId);
        foreach (var cancellation in cancellations)
        {
            result.TryAdd(cancellation.OperationId, cancellation.CartId);
        }

        return result;
    }

    private async Task<Dictionary<TopUpRequestId, PaymentMethod>> LoadTopUpPaymentMethodsAsync(
        TopUpRequestId[] topUpIds, CancellationToken ct)
    {
        if (topUpIds.Length == 0)
        {
            return [];
        }

        return await db.Set<TopUpRequest>()
            .AsNoTracking()
            .Where(topUp => topUpIds.Contains(topUp.Id))
            .Select(topUp => new { topUp.Id, topUp.PaymentMethod })
            .ToDictionaryAsync(topUp => topUp.Id, topUp => topUp.PaymentMethod, ct);
    }
}
