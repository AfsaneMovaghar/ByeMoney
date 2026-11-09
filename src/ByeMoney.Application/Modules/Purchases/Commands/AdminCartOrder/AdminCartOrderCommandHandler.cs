using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Services;
using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Carts.Services;
using ByeMoney.Application.Modules.Purchases.Constants;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.AdminCartOrder;

public sealed class AdminCartOrderCommandHandler(
    CartService carts,
    UserProfileSyncService userSync,
    ICurrentUserService currentUser) : IRequestHandler<AdminCartOrderCommand, Result<CartResponse>>
{
    public async Task<Result<CartResponse>> Handle(AdminCartOrderCommand request, CancellationToken ct)
    {
        var adminUserId = currentUser.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);
        var buyerId = await userSync.SyncAsync(request.BeneficiaryExternalUserId, forceSync: false, ct);
        var freeReason = request.IsFree ? request.FreeReason : null;

        var prepared = await carts.PrepareAdminOrderAsync(
            buyerId, request.CartId, request.Items!, adminUserId, freeReason, ct);
        if (prepared.IsFailure)
        {
            return prepared;
        }

        var paid = await carts.PurchaseAsync(buyerId, request.CartId, ct);
        if (paid.ErrorCode != CartErrorCodes.InsufficientBalance)
        {
            return paid;
        }

        // این مسیر شارژ نمی‌سازد؛ رزرو استرپی همین‌جا آزاد می‌شود و تکرار همان سبد پس از شارژ، quote تازه می‌گیرد.
        await carts.CancelAsync(buyerId, request.CartId, ct);
        return Result<CartResponse>.Conflict(
            AdminCartOrderErrors.InsufficientNoorBalance, PurchaseErrorCodes.InsufficientNoorBalance);
    }
}
