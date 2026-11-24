using ByeMoney.Application.Modules.Wallet.Withdrawals.Constants;
using ByeMoney.Application.Modules.Wallet.Withdrawals.DTOs;
using ByeMoney.Application.Modules.Wallet.Withdrawals.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Wallet;
using ByeMoney.Domain.Modules.Wallet.Withdrawals;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetNoorWithdrawal;

public sealed class GetNoorWithdrawalQueryHandler(INoorWithdrawalStore store) :
    IRequestHandler<GetNoorWithdrawalQuery, Result<NoorWithdrawalResponse>>
{
    public async Task<Result<NoorWithdrawalResponse>> Handle(GetNoorWithdrawalQuery request, CancellationToken ct)
    {
        var response = await store.GetResponseAsync(new NoorWithdrawalId(request.WithdrawalId), FinancialScope.TarheElahi, ct);

        return response is null
            ? Result<NoorWithdrawalResponse>.NotFound(NoorWithdrawalErrors.NotFound, NoorWithdrawalErrorCodes.NotFound)
            : Result<NoorWithdrawalResponse>.Success(response);
    }
}
