using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Resources;

namespace ByeMoney.Domain.Modules.Wallet;

/// <summary>
/// مرز مالی سایت. فاز اول فقط طرح الهی را از مسیرهای ورودی می‌نویسد؛ مقدار دیگر در دامنه رد می‌شود.
/// </summary>
public static class FinancialScope
{
    public const string TarheElahi = "TARHE_ELAHI";
    public const string BuyMoney = "BUYMONEY";
    public const int MaxLength = 20;

    public static string Require(string? scopeId)
    {
        if (scopeId is not (TarheElahi or BuyMoney))
        {
            throw new DomainException(DomainErrors.FinancialScope_Invalid);
        }

        return scopeId;
    }

    public static string Check(string column = "ScopeId") =>
        $"\"{column}\" IN ('{TarheElahi}', '{BuyMoney}')";
}
