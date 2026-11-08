using ByeMoney.Application.Modules.Wallet.Withdrawals.DTOs;
using ByeMoney.Application.Modules.Wallet.Withdrawals.Services;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Queries.GetNoorWithdrawalReceipt;

public sealed class GetNoorWithdrawalReceiptQueryHandler(NoorWithdrawalService service) :
    IRequestHandler<GetNoorWithdrawalReceiptQuery, NoorWithdrawalReceiptFile?>
{
    public Task<NoorWithdrawalReceiptFile?> Handle(GetNoorWithdrawalReceiptQuery request, CancellationToken ct) =>
        service.OpenReceiptAsync(request.WithdrawalId, ct);
}
