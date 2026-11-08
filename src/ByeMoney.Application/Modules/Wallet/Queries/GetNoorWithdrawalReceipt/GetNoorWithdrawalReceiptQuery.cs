using ByeMoney.Application.Modules.Wallet.Withdrawals.DTOs;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetNoorWithdrawalReceipt;

/// <summary>فیش واریز یک خروج؛ اگر خروج یا فیش وجود نداشته باشد null.</summary>
public sealed record GetNoorWithdrawalReceiptQuery(Guid WithdrawalId) : IRequest<NoorWithdrawalReceiptFile?>;
