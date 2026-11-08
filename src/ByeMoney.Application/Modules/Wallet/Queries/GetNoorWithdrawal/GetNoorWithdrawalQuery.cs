using ByeMoney.Application.Modules.Wallet.Withdrawals.DTOs;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetNoorWithdrawal;

public sealed record GetNoorWithdrawalQuery(Guid WithdrawalId) : IRequest<Result<NoorWithdrawalResponse>>;
