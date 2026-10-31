using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Carts.Services;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.PurchaseCart;

public sealed class PurchaseCartCommandHandler(CartService service, ICurrentUserService currentUser) :
    IRequestHandler<PurchaseCartCommand, Result<CartResponse>>
{
    private Guid Buyer => currentUser.UserId ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public Task<Result<CartResponse>> Handle(PurchaseCartCommand request, CancellationToken ct) =>
        service.PurchaseAsync(Buyer, request.CartId, ct);
}
