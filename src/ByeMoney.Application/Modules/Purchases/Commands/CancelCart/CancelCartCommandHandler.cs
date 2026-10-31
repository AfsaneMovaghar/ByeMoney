using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Carts.Services;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.CancelCart;

public sealed class CancelCartCommandHandler(CartService service, ICurrentUserService currentUser) :
    IRequestHandler<CancelCartCommand, Result<CartResponse>>
{
    private Guid Buyer => currentUser.UserId ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public Task<Result<CartResponse>> Handle(CancelCartCommand request, CancellationToken ct) =>
        service.CancelAsync(Buyer, request.CartId, ct);
}
