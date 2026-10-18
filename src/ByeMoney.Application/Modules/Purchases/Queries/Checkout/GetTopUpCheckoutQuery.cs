using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Cart;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using FluentValidation;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.Checkout;

public sealed record GetTopUpCheckoutQuery(Guid TopUpId) : IRequest<TopUpCheckoutResponse>;
public sealed record CheckoutCourseResponse(string ExternalCourseId, string PurchaseStatus);
public sealed record TopUpCheckoutResponse(
    Guid TopUpId, string TopUpStatus, IReadOnlyList<CheckoutCourseResponse> Courses, Guid? CartId = null);

public sealed class GetTopUpCheckoutQueryValidator : AbstractValidator<GetTopUpCheckoutQuery>
{
    public GetTopUpCheckoutQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => x.TopUpId).NotEmpty().WithMessage(ApplicationErrors.TopUpRequest_NotFound);
    }
}

public sealed class GetTopUpCheckoutQueryHandler(
    ICurrentUserService currentUser, ITopUpRequestRepository topUps, ICoursePurchaseRepository purchases, ICourseCartStore? carts = null)
    : IRequestHandler<GetTopUpCheckoutQuery, TopUpCheckoutResponse>
{
    public async Task<TopUpCheckoutResponse> Handle(GetTopUpCheckoutQuery request, CancellationToken ct)
    {
        var userId = new UserId(currentUser.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated));
        var topUp = await topUps.GetByIdAsync(new TopUpRequestId(request.TopUpId), ct);
        if (topUp is null || topUp.UserId != userId)
            throw new NotFoundException(string.Format(ApplicationErrors.TopUpRequest_NotFound, request.TopUpId));
        var courses = new List<CheckoutCourseResponse>();
        foreach (var item in topUp.PendingItems.Where(i => i.ItemType == PendingItemType.Course))
        {
            var purchase = await purchases.GetByBuyerAndCourseAsync(userId, item.ExternalId, ct);
            var status = GetCourseCheckoutQueryHandler.PurchaseStatus(purchase);
            if (status == CheckoutStates.NotPurchased && topUp.Status == TopUpStatus.Confirmed)
                status = CheckoutStates.PurchaseIncomplete;
            courses.Add(new(item.ExternalId, status));
        }
        var cart = carts is null ? null : await carts.FindByTopUpAsync(topUp.Id.Value, userId.Value, ct);
        return new(topUp.Id.Value, topUp.Status.ToString(), courses, cart?.Id);
    }
}
