using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common.Generic;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.CreateAdminCardToCardTopUp;

public sealed class CreateAdminCardToCardTopUpCommandHandler(IAdminAssistedTopUpService topUps)
    : IRequestHandler<CreateAdminCardToCardTopUpCommand, Result<CreateTopUpRequestResponse>>
{
    public Task<Result<CreateTopUpRequestResponse>> Handle(CreateAdminCardToCardTopUpCommand request, CancellationToken ct)
        => topUps.CreateAsync(request, ct);
}
