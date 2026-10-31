using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Carts.Services;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.CancelCourseCart;

public sealed class CancelCourseCartCommandHandler(CartService service, ICurrentUserService currentUser) :
    IRequestHandler<CancelCourseCartCommand, Result<CartResponse>>
{
    private Guid Buyer => currentUser.UserId ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public Task<Result<CartResponse>> Handle(CancelCourseCartCommand request, CancellationToken ct) =>
        service.CancelAsync(Buyer, request.CartId, ct);
}
