using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.TopUps;

namespace ByeMoney.Application.Modules.Wallet.Services;

public sealed class TopUpSettlementService(
    IUserWalletProvisioningService provisioning,
    IWalletPostingService postingService,
    IUnitOfWork unitOfWork) : ITopUpSettlementService
{
    public async Task SettleAsync(TopUpRequest topUp, CancellationToken ct = default)
    {
        var user = await provisioning.GetOrCreateUserWalletAsync(topUp.UserId, ct);
        var system = await provisioning.GetOrCreateSystemAccountAsync(ct);
        var transactionId = Guid.NewGuid();

        await postingService.PostCreditAsync(
            user,
            system,
            topUp.AmountNoor,
            LedgerBalanceType.Regular,
            transactionId,
            LedgerReferenceType.TopUp,
            topUp.Id.ToString(),
            topUp.CreatedByUserId,
            ct);

        await unitOfWork.SaveChangesAsync(ct);
    }
}
