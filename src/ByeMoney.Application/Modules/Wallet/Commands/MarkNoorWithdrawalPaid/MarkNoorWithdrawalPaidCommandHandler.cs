using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Withdrawals.DTOs;
using ByeMoney.Application.Modules.Wallet.Withdrawals.Services;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.MarkNoorWithdrawalPaid;

public sealed class MarkNoorWithdrawalPaidCommandHandler(
    NoorWithdrawalService service,
    ICurrentUserService currentUser) :
    IRequestHandler<MarkNoorWithdrawalPaidCommand, Result<NoorWithdrawalResponse>>
{
    public Task<Result<NoorWithdrawalResponse>> Handle(MarkNoorWithdrawalPaidCommand request, CancellationToken ct)
    {
        var adminId = currentUser.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

        return service.MarkPaidAsync(adminId, request.WithdrawalId, request.PaymentReference, request.ReceiptId, ct);
    }
}
