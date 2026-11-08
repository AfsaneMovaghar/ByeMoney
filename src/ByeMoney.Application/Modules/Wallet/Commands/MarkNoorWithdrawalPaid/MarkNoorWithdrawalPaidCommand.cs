using ByeMoney.Application.Modules.Wallet.Withdrawals.DTOs;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.MarkNoorWithdrawalPaid;

/// <summary>ثبت واریز بانکی یک خروج در انتظار، با شناسهٔ پرداخت و فیش؛ اثر Ledger ندارد.</summary>
public sealed record MarkNoorWithdrawalPaidCommand(Guid WithdrawalId, string PaymentReference, string ReceiptId) :
    IRequest<Result<NoorWithdrawalResponse>>;
