using ByeMoney.Application.Modules.Wallet.Withdrawals.DTOs;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetNoorWithdrawals;

/// <summary>فهرست خروج‌های نور، جدیدترین اول؛ وضعیت و کاربر اختیاری‌اند.</summary>
public sealed record GetNoorWithdrawalsQuery(string? Status, string? UserExternalId, int Limit = GetNoorWithdrawalsQuery.DefaultLimit) :
    IRequest<IReadOnlyList<NoorWithdrawalResponse>>
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;
}
