using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Constants;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Purchases;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.ConfirmMyPhysicalDelivery;

/// <summary>
/// خریدار دریافت کالا را تأیید می‌کند: همهٔ اقلام Shipped همان سفارش خودش Delivered می‌شوند.
/// سفارش کاربر دیگر یا شناسهٔ نامعتبر ۴۰۴ است؛ تکرار عملیات بی‌اثر و بدون خطاست.
/// </summary>
public sealed class ConfirmMyPhysicalDeliveryCommandHandler(
    IRepository<PhysicalProductPurchase, PhysicalProductPurchaseId> repository,
    IPhysicalDeliveryStore deliveryStore,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) :
    IRequestHandler<ConfirmMyPhysicalDeliveryCommand, PhysicalDeliveryConfirmationResponse>
{
    public async Task<PhysicalDeliveryConfirmationResponse> Handle(
        ConfirmMyPhysicalDeliveryCommand request, CancellationToken ct)
    {
        var buyerId = currentUser.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

        if (!OrderIdFormat.TryParse(request.OrderId, out var source, out var orderId) ||
            source == OrderSourceType.TopUpCheckout)
        {
            throw new NotFoundException(OrderErrors.NotFound);
        }

        var owner = source == OrderSourceType.Cart
            ? await deliveryStore.FindCartOwnerAsync(orderId, ct)
            : await deliveryStore.FindOwnerAsync(orderId, ct);
        if (owner is null || owner.BuyerId != buyerId)
        {
            throw new NotFoundException(OrderErrors.NotFound);
        }

        // خواندن و ذخیره زیر قفل خریدار و سبد انجام می‌شود تا با ارسال/لغو هم‌زمان ناسازگار نشود.
        return await deliveryStore.ExecuteLockedAsync(owner.BuyerId, owner.CartId, async () =>
        {
            var purchases = source == OrderSourceType.Cart
                ? await deliveryStore.GetPurchasesAsync(owner.CartId, ct)
                : await LoadPurchaseAsync(orderId, ct);

            var shipped = purchases
                .Where(purchase => purchase.BuyerId.Value == buyerId &&
                    purchase.DeliveryStatus == PhysicalDeliveryStatus.Shipped)
                .ToArray();

            foreach (var purchase in shipped)
            {
                purchase.MarkDelivered();
                repository.Update(purchase);
            }

            if (shipped.Length > 0)
            {
                await unitOfWork.SaveChangesAsync(ct);
            }

            return new PhysicalDeliveryConfirmationResponse(
                request.OrderId, shipped.Length, shipped.Select(purchase => purchase.Id.Value).ToArray());
        }, ct);
    }

    private async Task<IReadOnlyList<PhysicalProductPurchase>> LoadPurchaseAsync(Guid purchaseId, CancellationToken ct)
    {
        var purchase = await repository.GetByIdAsync(new PhysicalProductPurchaseId(purchaseId), ct);
        return purchase is null ? [] : [purchase];
    }
}
