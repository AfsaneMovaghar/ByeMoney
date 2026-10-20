using ByeMoney.Application.Modules.Purchases.Cart.DTOs;
using ByeMoney.Application.Modules.Purchases.Cart.Services;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.PrepareCourseCart;

public sealed class PrepareCourseCartCommandHandler(CourseCartService service, ICurrentUserService currentUser) :
    IRequestHandler<PrepareCourseCartCommand, Result<CartResponse>>
{
    private Guid Buyer => currentUser.UserId ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public Task<Result<CartResponse>> Handle(PrepareCourseCartCommand request, CancellationToken ct) =>
        service.PrepareAsync(Buyer, request.CartId, request.ExternalCourseIds, request.CouponCode, ct);
}
