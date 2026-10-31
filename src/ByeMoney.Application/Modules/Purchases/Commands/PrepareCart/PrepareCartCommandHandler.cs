using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Carts.Services;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Purchases;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.PrepareCart;

public sealed class PrepareCartCommandHandler(CartService service, ICurrentUserService currentUser) :
    IRequestHandler<PrepareCartCommand, Result<CartResponse>>
{
    private Guid Buyer => currentUser.UserId ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public Task<Result<CartResponse>> Handle(PrepareCartCommand request, CancellationToken ct)
    {
        var items = request.Items ?? request.ExternalCourseIds?.Select(x => new CartItemInput(x, ProductItemType.Course, 1)).ToArray() ?? [];
        return service.PrepareAsync(Buyer, request.CartId, items, request.CouponCode, ct);
    }
}

