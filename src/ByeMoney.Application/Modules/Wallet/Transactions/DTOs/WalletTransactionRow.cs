using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.TopUps;

namespace ByeMoney.Application.Modules.Wallet.Transactions.DTOs;

/// <summary>
/// یک تراکنش متوازن روی حساب کاربر؛ ردیف‌های عادی و هدیهٔ همان تراکنش جمع شده‌اند.
/// </summary>
public sealed record WalletTransactionRow(
    Guid TransactionId,
    LedgerReferenceType ReferenceType,
    string? ReferenceId,
    decimal AmountNoor,
    decimal GiftAmountNoor,
    DateTime CreatedAtUtc,
    Guid? OrderCartId,
    PaymentMethod? PaymentMethod = null);
