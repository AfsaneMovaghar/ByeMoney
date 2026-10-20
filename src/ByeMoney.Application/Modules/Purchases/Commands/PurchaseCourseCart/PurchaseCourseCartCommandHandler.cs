using ByeMoney.Application.Modules.Purchases.Cart.DTOs;
using ByeMoney.Application.Modules.Purchases.Cart.Services;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourseCart;

public sealed class PurchaseCourseCartCommandHandler(CourseCartService service, ICurrentUserService currentUser) :
    IRequestHandler<PurchaseCourseCartCommand, Result<CartResponse>>
{
    private Guid Buyer => currentUser.UserId ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public Task<Result<CartResponse>> Handle(PurchaseCourseCartCommand request, CancellationToken ct) =>
        service.PurchaseAsync(Buyer, request.CartId, ct);
}
