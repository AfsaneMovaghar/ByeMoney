namespace ByeMoney.Domain.Modules.Wallet.Withdrawals;

public enum NoorWithdrawalStatus
{
    /// <summary>نور کسر شده و واریز بانکی هنوز ثبت نشده است.</summary>
    PendingPayment = 1,

    /// <summary>واریز بانکی با مرجع و فیش ثبت شده است.</summary>
    Paid = 2,

    /// <summary>واریز انجام نشد و نور با ثبت جبرانی به کاربر برگشت.</summary>
    Reversed = 3
}
