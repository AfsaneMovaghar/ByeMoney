using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Withdrawals.DTOs;
using ByeMoney.Application.Modules.Wallet.Withdrawals.Services;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.ReverseNoorWithdrawal;

public sealed class ReverseNoorWithdrawalCommandHandler(
    NoorWithdrawalService service,
    ICurrentUserService currentUser) :
    IRequestHandler<ReverseNoorWithdrawalCommand, Result<NoorWithdrawalResponse>>
{
    public Task<Result<NoorWithdrawalResponse>> Handle(ReverseNoorWithdrawalCommand request, CancellationToken ct)
    {
        var adminId = currentUser.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

        return service.ReverseAsync(adminId, request.WithdrawalId, request.Reason, ct);
    }
}
