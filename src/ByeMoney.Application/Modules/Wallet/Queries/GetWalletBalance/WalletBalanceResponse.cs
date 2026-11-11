namespace ByeMoney.Application.Modules.Wallet.Queries.GetWalletBalance;

/// <param name="Balance">کل موجودی (عادی + هدیه).</param>
/// <param name="GiftBalance">سهم نور هدیه از کل موجودی؛ فقط برای خرید مصرف می‌شود و قابل خروج نیست.</param>
public record WalletBalanceResponse(
    decimal Balance,
    string Currency = "Noor",
    decimal GiftBalance = 0m);
