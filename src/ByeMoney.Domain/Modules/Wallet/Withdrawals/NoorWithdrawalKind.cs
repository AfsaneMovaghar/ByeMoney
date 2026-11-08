namespace ByeMoney.Domain.Modules.Wallet.Withdrawals;

/// <summary>علت خروج نور توسط ادمین (D4).</summary>
public enum NoorWithdrawalKind
{
    /// <summary>خروج بخشی از نور عادی کاربر به صلاحدید ادمین.</summary>
    AdminDiscretionary = 1,

    /// <summary>استرداد وجه پرداخت تکراری درگاه؛ همیشه به همان شارژ تأییدشده وصل است.</summary>
    DuplicatePaymentRefund = 2
}
