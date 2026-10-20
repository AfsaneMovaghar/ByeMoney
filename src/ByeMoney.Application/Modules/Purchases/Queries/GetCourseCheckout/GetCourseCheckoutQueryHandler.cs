using ByeMoney.Application.Modules.Purchases.Constants;
using ByeMoney.Application.Modules.Settings.Interfaces;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetCourseCheckout;

public sealed class GetCourseCheckoutQueryHandler(
    ICurrentUserService currentUser, ITarhElahiIntegrationClient catalog,
    IWalletRepository wallets, ISystemSettingRepository settings, ICoursePurchaseRepository purchases)
    : IRequestHandler<GetCourseCheckoutQuery, CourseCheckoutResponse>
{
    public async Task<CourseCheckoutResponse> Handle(GetCourseCheckoutQuery request, CancellationToken ct)
    {
        var userId = new UserId(currentUser.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated));
        var purchase = await purchases.GetByBuyerAndCourseAsync(userId, request.ExternalCourseId, ct);
        var wallet = await wallets.GetByUserIdAsync(userId, ct);
        var balance = wallet?.Balance ?? 0m;
        if (purchase is not null)
            return new(request.ExternalCourseId, purchase.Snapshot.PriceInNoorAtPurchaseTime, balance, 0m, 0m, PurchaseStatus(purchase));
        var course = await catalog.GetCourseAsync(request.ExternalCourseId, ct)
            ?? throw new NotFoundException(ApplicationErrors.CoursePurchase_CourseNotFound);
        if (!course.Published || !course.Available)
            throw new DomainException(ApplicationErrors.CoursePurchase_CourseNotAvailable);
        var shortfall = decimal.Ceiling(Math.Max(0m, course.PriceNoor - balance));
        var rate = shortfall > 0 ? await settings.GetRialToNoorConversionRateAsync(ct) : 0m;
        if (shortfall > 0 && (rate <= 0 || decimal.Truncate(rate) != rate))
            throw new DomainException(ApplicationErrors.TopUpRequest_InvalidRate);
        return new(course.ExternalId, course.PriceNoor, balance, shortfall, shortfall * rate,
            PurchaseStatus(purchase));
    }

    internal static string PurchaseStatus(CoursePurchase? purchase) => purchase?.Status switch
    {
        CoursePurchaseStatus.NotificationSent => CheckoutStates.Completed,
        CoursePurchaseStatus.NotificationFailed => CheckoutStates.DeliveryPending,
        CoursePurchaseStatus.Debited => CheckoutStates.Activating,
        _ => CheckoutStates.NotPurchased
    };
}
