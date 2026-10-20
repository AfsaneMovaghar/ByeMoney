using ByeMoney.Domain.Common.Generic;
using ByeMoney.Application.Modules.Wallet.Commands.CreateAdminCardToCardTopUp;

namespace ByeMoney.Application.Modules.Wallet.Interfaces;

public interface IAdminAssistedTopUpService
{
    Task<Result<CreateTopUpRequestResponse>> CreateAsync(CreateAdminCardToCardTopUpCommand request, CancellationToken ct);
    Task DeleteUnreferencedReceiptAsync(string receiptId, string idempotencyKey, CancellationToken ct);
}
