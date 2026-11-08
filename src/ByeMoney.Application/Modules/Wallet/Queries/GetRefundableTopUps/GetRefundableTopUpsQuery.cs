using ByeMoney.Application.Modules.Wallet.Withdrawals.DTOs;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetRefundableTopUps;

/// <summary>شارژهای تأییدشدهٔ یک کاربر برای انتخاب در فرم استرداد پرداخت تکراری، جدیدترین اول.</summary>
public sealed record GetRefundableTopUpsQuery(string UserExternalId) :
    IRequest<IReadOnlyList<RefundableTopUpResponse>>
{
    public const int MaxResults = 50;
}
