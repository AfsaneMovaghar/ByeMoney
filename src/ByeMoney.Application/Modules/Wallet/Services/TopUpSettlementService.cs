using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.TopUps;

namespace ByeMoney.Application.Modules.Wallet.Services;

/// <summary>
/// تسویهٔ شارژ؛ تنها طبقه‌بندی مالی شارژ را تعیین می‌کند (ماندهٔ عادی و مرجع شارژ) و افزایش نور را
/// به سرویس واحد نور می‌سپارد. مرز تراکنش همان‌جاست که بود: یک ذخیره در پایان همین متد.
/// </summary>
public sealed class TopUpSettlementService(
    INoorCreditService noorCreditService,
    IUnitOfWork unitOfWork) : ITopUpSettlementService
{
    public async Task SettleAsync(TopUpRequest topUp, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(topUp);

        await noorCreditService.CreditAsync(
            new NoorCreditRequest(
                UserId: topUp.UserId,
                AmountNoor: topUp.AmountNoor,
                BalanceType: LedgerBalanceType.Regular,
                ReferenceType: LedgerReferenceType.TopUp,
                ReferenceId: topUp.Id.ToString(),
                PerformedByUserId: topUp.CreatedByUserId),
            ct);

        await unitOfWork.SaveChangesAsync(ct);
    }
}
