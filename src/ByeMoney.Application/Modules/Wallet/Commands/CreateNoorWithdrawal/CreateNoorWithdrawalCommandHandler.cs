using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Withdrawals.DTOs;
using ByeMoney.Application.Modules.Wallet.Withdrawals.Services;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Wallet;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.CreateNoorWithdrawal;

public sealed class CreateNoorWithdrawalCommandHandler(
    NoorWithdrawalService service,
    ICurrentUserService currentUser) :
    IRequestHandler<CreateNoorWithdrawalCommand, Result<NoorWithdrawalResponse>>
{
    public Task<Result<NoorWithdrawalResponse>> Handle(CreateNoorWithdrawalCommand request, CancellationToken ct)
    {
        var adminId = currentUser.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

        return service.CreateAsync(adminId, request, FinancialScope.TarheElahi, ct);
    }
}
