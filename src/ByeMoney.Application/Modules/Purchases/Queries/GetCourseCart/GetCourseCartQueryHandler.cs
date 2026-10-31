using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Carts.Services;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetCourseCart;

public sealed class GetCourseCartQueryHandler(CourseCartService service, ICurrentUserService currentUser) :
    IRequestHandler<GetCourseCartQuery, Result<CartResponse>>
{
    private Guid Buyer => currentUser.UserId ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public Task<Result<CartResponse>> Handle(GetCourseCartQuery request, CancellationToken ct) =>
        service.GetAsync(Buyer, request.CartId, ct);
}
