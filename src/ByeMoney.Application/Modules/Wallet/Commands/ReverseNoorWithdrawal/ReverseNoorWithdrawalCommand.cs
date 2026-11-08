using ByeMoney.Application.Modules.Wallet.Withdrawals.DTOs;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.ReverseNoorWithdrawal;

/// <summary>«پرداخت نشد»: نور یک خروج در انتظار با ثبت جبرانی مرتبط به نور عادی کاربر برمی‌گردد.</summary>
public sealed record ReverseNoorWithdrawalCommand(Guid WithdrawalId, string Reason) :
    IRequest<Result<NoorWithdrawalResponse>>;
