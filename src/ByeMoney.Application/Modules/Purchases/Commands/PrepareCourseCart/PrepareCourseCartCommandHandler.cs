using ByeMoney.Application.Modules.Purchases.Cart.DTOs;
using ByeMoney.Application.Modules.Purchases.Cart.Services;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Purchases;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.PrepareCourseCart;

public sealed class PrepareCourseCartCommandHandler(CourseCartService service, ICurrentUserService currentUser) :
    IRequestHandler<PrepareCourseCartCommand, Result<CartResponse>>
{
    private Guid Buyer => currentUser.UserId ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public Task<Result<CartResponse>> Handle(PrepareCourseCartCommand request, CancellationToken ct)
    {
        var items = request.Items ?? request.ExternalCourseIds?.Select(x => new CartItemInput(x, ProductItemType.Course, 1)).ToArray() ?? [];
        return service.PrepareAsync(Buyer, request.CartId, items, request.CouponCode, ct);
    }
}
