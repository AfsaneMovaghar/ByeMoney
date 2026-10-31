using ByeMoney.Application.Modules.Purchases.Carts.Services;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;
using ByeMoney.Application.Resources;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.CreateCartTopUp;

public sealed class CreateCartTopUpCommandHandler(CartService service, ICurrentUserService currentUser) :
    IRequestHandler<CreateCartTopUpCommand, Result<CreateTopUpRequestResponse>>
{
    private Guid Buyer => currentUser.UserId ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public Task<Result<CreateTopUpRequestResponse>> Handle(CreateCartTopUpCommand request, CancellationToken ct) =>
        service.TopUpAsync(Buyer, request.CartId, ct);
}
