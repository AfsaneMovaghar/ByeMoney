using System.Security.Cryptography;
using System.Text;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Commands.SyncUserFromStrapi;
using ByeMoney.Application.Modules.Identity.Users.Interfaces;
using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Gifts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.AdminGrantGiftNoor;

public sealed class AdminGrantGiftNoorCommandHandler(
    IUserRepository userRepository,
    IGiftGrantRepository giftGrantRepository,
    INoorCreditService noorCreditService,
    IUnitOfWork unitOfWork,
    ISender sender) : IRequestHandler<AdminGrantGiftNoorCommand, AdminGrantGiftNoorResult>
{
    public async Task<AdminGrantGiftNoorResult> Handle(AdminGrantGiftNoorCommand request, CancellationToken ct)
    {
        var payloadHash = ComputePayloadHash(request);

        // ۱. بررسی تکرار کلید تکرارناپذیری (Idempotency)
        var existingBatch = await giftGrantRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey, ct);
        if (existingBatch is not null)
        {
            if (string.Equals(existingBatch.RequestPayloadHash, payloadHash, StringComparison.Ordinal))
            {
                var cachedRecipients = existingBatch.Recipients
                    .Select(r => new GiftRecipientResponseDto(
                        r.ExternalUserId,
                        r.UserId.Value,
                        r.Status.ToString(),
                        r.NewGiftBalance))
                    .ToList();

                var cachedResponse = new AdminGrantGiftNoorResponse(
                    existingBatch.Id,
                    existingBatch.AmountNoorPerUser,
                    existingBatch.TotalRecipients,
                    existingBatch.TotalAmountNoor,
                    existingBatch.Reason,
                    existingBatch.CreatedAtUtc,
                    cachedRecipients);

                return AdminGrantGiftNoorResult.Success(cachedResponse);
            }

            return AdminGrantGiftNoorResult.Conflict(ApplicationErrors.TopUpRequest_IdempotencyConflict);
        }

        // ۲. همگام‌سازی ذی‌نفع‌های ناموجود با Strapi (بیرون از تراکنش دیتابیس)
        var invalidUsers = new List<InvalidGiftUserDto>();
        var targetUsers = new List<User>();

        foreach (var externalUserId in request.ExternalUserIds)
        {
            var user = await userRepository.GetByExternalUserIdAsync(externalUserId, ct);
            if (user is null)
            {
                try
                {
                    await sender.Send(new SyncUserFromStrapiCommand(externalUserId), ct);
                    user = await userRepository.GetByExternalUserIdAsync(externalUserId, ct);
                }
                catch (NotFoundException)
                {
                    invalidUsers.Add(new InvalidGiftUserDto(externalUserId, ApplicationErrors.Gift_UserNotFound));
                    continue;
                }
            }

            if (user is null)
            {
                invalidUsers.Add(new InvalidGiftUserDto(externalUserId, ApplicationErrors.Gift_UserNotFound));
                continue;
            }

            if (!user.IsActive)
            {
                invalidUsers.Add(new InvalidGiftUserDto(externalUserId, ApplicationErrors.Gift_UserBlocked));
                continue;
            }

            targetUsers.Add(user);
        }

        // ۳. بررسی همه‌یاهیچ: اگر حتی یک کاربر مشکل‌دار بود، هیچ ثبتی انجام نمی‌شود
        if (invalidUsers.Count > 0)
        {
            return AdminGrantGiftNoorResult.WithInvalidUsers(invalidUsers, ApplicationErrors.Gift_HasInvalidUsers);
        }

        // ۴. ثبت در یک تراکنش واحد و متوازن با استفاده از سرویس واحد افزایش نور
        var adminUserId = request.AdminUserId.HasValue ? (UserId?)new UserId(request.AdminUserId.Value) : null;
        var grantedAtUtc = DateTime.UtcNow;

        var batch = GiftGrantBatch.Create(
            request.IdempotencyKey,
            payloadHash,
            GiftGrantSource.AdminBatch,
            request.Reason,
            request.AmountNoor,
            adminUserId,
            grantedAtUtc);

        var recipientResponses = new List<GiftRecipientResponseDto>(targetUsers.Count);

        foreach (var user in targetUsers)
        {
            var creditResult = await noorCreditService.CreditAsync(new NoorCreditRequest(
                UserId: user.Id,
                AmountNoor: request.AmountNoor,
                BalanceType: LedgerBalanceType.Gift,
                ReferenceType: LedgerReferenceType.AdminGrant,
                ReferenceId: batch.Id.ToString(),
                PerformedByUserId: adminUserId,
                Reason: request.Reason), ct);

            var recipient = GiftGrantRecipient.Create(
                batch.Id,
                user.Id,
                user.ExternalUserId,
                creditResult.AmountCredited,
                creditResult.TransactionId,
                creditResult.NewGiftBalance,
                grantedAtUtc,
                GiftRecipientStatus.Granted);

            batch.AddRecipient(recipient);

            recipientResponses.Add(new GiftRecipientResponseDto(
                user.ExternalUserId,
                user.Id.Value,
                recipient.Status.ToString(),
                creditResult.NewGiftBalance));
        }

        await giftGrantRepository.AddBatchAsync(batch, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var response = new AdminGrantGiftNoorResponse(
            batch.Id,
            batch.AmountNoorPerUser,
            batch.TotalRecipients,
            batch.TotalAmountNoor,
            batch.Reason,
            batch.CreatedAtUtc,
            recipientResponses);

        return AdminGrantGiftNoorResult.Success(response);
    }

    private static string ComputePayloadHash(AdminGrantGiftNoorCommand command)
    {
        var sortedIds = string.Join("|", command.ExternalUserIds.OrderBy(x => x, StringComparer.Ordinal));
        var raw = $"{command.AmountNoor:F0};{command.Reason};{sortedIds}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

