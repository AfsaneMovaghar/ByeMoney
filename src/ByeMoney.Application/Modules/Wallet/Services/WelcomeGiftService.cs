using System.Security.Cryptography;
using System.Text;
using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Gifts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;

namespace ByeMoney.Application.Modules.Wallet.Services;

public sealed class WelcomeGiftService(
    IGiftCampaignSettingRepository giftSettingRepository,
    IGiftGrantRepository giftGrantRepository,
    INoorCreditService noorCreditService,
    IUserWalletProvisioningService provisioningService) : IWelcomeGiftService
{
    public async Task GrantWelcomeGiftIfEligibleAsync(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var nowUtc = DateTime.UtcNow;
        var welcomeSetting = await giftSettingRepository.GetByTypeAsync(GiftCampaignType.Welcome, ct);

        if (welcomeSetting is not null && welcomeSetting.IsActiveAt(nowUtc))
        {
            var amount = welcomeSetting.AmountNoor;
            var idempotencyKey = $"welcome-bonus:{user.Id.Value}";
            var payloadHash = ComputePayloadHash(amount, user.ExternalUserId);
            var reason = ApplicationErrors.Gift_WelcomeBonusReason;

            var batch = GiftGrantBatch.Create(
                idempotencyKey,
                requestPayloadHash: payloadHash,
                GiftGrantSource.WelcomeBonus,
                reason: reason,
                amountNoorPerUser: amount,
                adminUserId: null,
                createdAtUtc: nowUtc);

            var creditResult = await noorCreditService.CreditAsync(new NoorCreditRequest(
                UserId: user.Id,
                AmountNoor: amount,
                BalanceType: LedgerBalanceType.Gift,
                ReferenceType: LedgerReferenceType.WelcomeGift,
                ReferenceId: batch.Id.ToString(),
                PerformedByUserId: null,
                Reason: reason), ct);

            var recipient = GiftGrantRecipient.Create(
                batch.Id,
                user.Id,
                user.ExternalUserId,
                creditResult.AmountCredited,
                creditResult.TransactionId,
                creditResult.NewGiftBalance,
                nowUtc,
                GiftRecipientStatus.Granted);

            batch.AddRecipient(recipient);
            await giftGrantRepository.AddBatchAsync(batch, ct);
        }
        else
        {
            await provisioningService.GetOrCreateUserWalletAsync(user.Id, ct);
        }
    }

    private static string ComputePayloadHash(decimal amount, string externalUserId)
    {
        var raw = $"{amount:F0};WelcomeBonus;{externalUserId}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

