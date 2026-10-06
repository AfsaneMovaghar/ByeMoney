using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Commands.SyncUserFromStrapi;
using ByeMoney.Application.Modules.Identity.Users.Interface;
using ByeMoney.Application.Modules.Settings;
using ByeMoney.Application.Modules.Wallet.Commands.CreateAdminCardToCardTopUp;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Services;

public interface IAdminAssistedTopUpService
{
    Task<Result<CreateTopUpRequestResponse>> CreateAsync(CreateAdminCardToCardTopUpCommand request, CancellationToken ct);
    Task DeleteUnreferencedReceiptAsync(string receiptId, string idempotencyKey, CancellationToken ct);
}

public sealed class AdminAssistedTopUpService(
    IUserRepository users,
    ISystemSettingRepository settings,
    ITopUpRequestRepository topUps,
    ITopUpSettlementService settlement,
    IReceiptStorage receipts,
    ISender sender) : IAdminAssistedTopUpService
{
    public async Task<Result<CreateTopUpRequestResponse>> CreateAsync(CreateAdminCardToCardTopUpCommand request, CancellationToken ct)
    {
        var existing = await topUps.GetByIdempotencyKeyAsync(request.IdempotencyKey, ct);
        if (existing is not null)
            return await ResolveRetryAsync(existing, request, ct);

        User? beneficiary;
        try
        {
            beneficiary = await GetOrSyncBeneficiaryAsync(request.BeneficiaryExternalUserId, ct);
        }
        catch (NotFoundException ex)
        {
            return Result<CreateTopUpRequestResponse>.NotFound(ex.Message);
        }

        if (beneficiary is not { IsActive: true })
            return Result<CreateTopUpRequestResponse>.NotFound(ApplicationErrors.TopUpRequest_InactiveBeneficiary);

        var rate = await settings.GetRialToNoorConversionRateAsync(ct);
        if (rate <= 0)
            return Result<CreateTopUpRequestResponse>.Failure(ApplicationErrors.TopUpRequest_InvalidRate);

        var topUp = CreateTopUp(request, beneficiary.Id, rate);
        await topUps.AddAsync(topUp, ct);
        await settlement.SettleAsync(topUp, ct);
        return Result<CreateTopUpRequestResponse>.Success(ToResponse(topUp));
    }

    private async Task<Result<CreateTopUpRequestResponse>> ResolveRetryAsync(
        TopUpRequest existing, CreateAdminCardToCardTopUpCommand request, CancellationToken ct)
    {
        var beneficiary = await users.GetByExternalUserIdAsync(request.BeneficiaryExternalUserId, ct);
        var matches = beneficiary is not null && existing.UserId == beneficiary.Id &&
            existing.CreatedByUserId == new UserId(request.ActorUserId) &&
            existing.AmountRial == checked(request.AmountToman * 10m) && existing.ReceiptId == request.ReceiptId &&
            existing.ChargeType == request.ChargeType && existing.ExternalTransactionId == request.ExternalTransactionId;

        return matches
            ? Result<CreateTopUpRequestResponse>.Success(ToResponse(existing))
            : Result<CreateTopUpRequestResponse>.Conflict(ApplicationErrors.TopUpRequest_IdempotencyConflict);
    }

    private async Task<User?> GetOrSyncBeneficiaryAsync(string externalUserId, CancellationToken ct)
    {
        var beneficiary = await users.GetByExternalUserIdAsync(externalUserId, ct);
        if (beneficiary is not null) return beneficiary;

        await sender.Send(new SyncUserFromStrapiCommand(externalUserId), ct);
        return await users.GetByExternalUserIdAsync(externalUserId, ct);
    }

    private static TopUpRequest CreateTopUp(CreateAdminCardToCardTopUpCommand request, UserId beneficiaryId, decimal rate)
        => request.ChargeType switch
        {
            ChargeType.AdminAssistedCardToCard => TopUpRequest.CreateAdminCardToCard(
                beneficiaryId, new UserId(request.ActorUserId), request.AmountToman, rate,
                request.ReceiptId, request.IdempotencyKey, request.ExternalTransactionId),
            _ => throw new InvalidOperationException(ApplicationErrors.TopUpRequest_UnsupportedChargeType)
        };

    public async Task DeleteUnreferencedReceiptAsync(string receiptId, string idempotencyKey, CancellationToken ct)
    {
        var existing = await topUps.GetByIdempotencyKeyAsync(idempotencyKey, ct);
        if (existing?.ReceiptId != receiptId)
            await receipts.DeleteAsync(receiptId, ct);
    }

    private static CreateTopUpRequestResponse ToResponse(TopUpRequest topUp)
        => new(topUp.Id.Value, topUp.ClientReferenceCode, topUp.AmountNoor, topUp.RialPerNoorSnapshot!.Value);
}
