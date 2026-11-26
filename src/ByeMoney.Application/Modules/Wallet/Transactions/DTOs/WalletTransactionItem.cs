namespace ByeMoney.Application.Modules.Wallet.Transactions.DTOs;

/// <summary>
/// ردیف گردش کیف پول کاربر. مبلغ با علامت به نور است (مثبت: واریز، منفی: برداشت)؛
/// <see cref="GiftAmountNoor"/> سهم نور هدیه از همان مبلغ است.
/// </summary>
public sealed record WalletTransactionItem(
    Guid TransactionId,
    string Type,
    decimal AmountNoor,
    decimal GiftAmountNoor,
    DateTime CreatedAtUtc,
    string? OrderId,
    string? TopUpId,
    string? PaymentMethod = null);
