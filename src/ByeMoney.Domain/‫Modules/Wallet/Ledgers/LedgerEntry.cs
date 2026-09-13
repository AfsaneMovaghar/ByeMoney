using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Wallet.Accounts;

namespace ByeMoney.Domain.Modules.Wallet.Ledgers;

public class LedgerEntry : BaseEntity<LedgerEntryId>
{
    /// <summary>شناسه حساب مقصد (حساب کاربر یا حساب یکتای سیستم).</summary>
    public AccountId AccountId { get; private set; }

    /// <summary>مبلغ با علامت به نور؛ مثبت برای بستانکار و منفی برای بدهکار (تراز صفر دوطرفه).</summary>
    public decimal Amount { get; private set; }

    /// <summary>شناسه ردیابی پیونددهنده جفت رکوردهای بستانکار و بدهکار در یک تراکنش متوازن.</summary>
    public Guid TransactionId { get; private set; }

    /// <summary>شناسه موجودیت مرجع صادرکننده سند (مانند شناسه TopUpRequest یا CoursePurchase).</summary>
    public string? ReferenceId { get; private set; }

    /// <summary>نوع مرجع سند در دفتر کل (مانند شارژ، خرید، برگشت وجه یا اعطای سیستمی).</summary>
    public LedgerReferenceType ReferenceType { get; private set; }

    private LedgerEntry() { }

    public static LedgerEntry Create(
        AccountId accountId,
        decimal amount,
        Guid transactionId,
        LedgerReferenceType referenceType,
        string? referenceId = null)
    {
        return new LedgerEntry
        {
            Id = LedgerEntryId.New(),
            AccountId = accountId,
            Amount = amount,
            TransactionId = transactionId,
            ReferenceId = referenceId,
            ReferenceType = referenceType
        };
    }
}