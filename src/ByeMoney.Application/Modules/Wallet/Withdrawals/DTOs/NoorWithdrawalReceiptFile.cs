namespace ByeMoney.Application.Modules.Wallet.Withdrawals.DTOs;

/// <summary>فایل فیش واریز یک خروج؛ پسوند شناسهٔ ذخیره نوع تصویر را مشخص می‌کند.</summary>
public sealed record NoorWithdrawalReceiptFile(Stream Content, string ReceiptId);
