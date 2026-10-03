using ByeMoney.Application.Modules.Wallet.Commands.CancelGatewayTopUp;
using ByeMoney.Application.Modules.Wallet.Commands.ReportGatewayCancellation;
using ByeMoney.Application.Modules.Wallet.Constants;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Domain.Resources;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ByeMoney.Infrastructure.Modules.Wallet.Services;

public sealed class ReportGatewayCancellationCommandHandler(ISender sender, IServiceScopeFactory scopes)
    : IRequestHandler<ReportGatewayCancellationCommand, Result>
{
    public async Task<Result> Handle(ReportGatewayCancellationCommand request, CancellationToken ct)
    {
        try
        {
            return await sender.Send(new CancelGatewayTopUpCommand(request.ClientReferenceCode), ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            using var scope = scopes.CreateScope();
            var fresh = await scope.ServiceProvider.GetRequiredService<ITopUpRequestRepository>()
                .GetByClientReferenceCodeAsync(request.ClientReferenceCode, ct);
            return fresh?.Status switch
            {
                TopUpStatus.Rejected when fresh.GatewayName == "SEP" => Result.Success(),
                TopUpStatus.Rejected => Result.Conflict(DomainErrors.TopUpRequest_CannotConfirmRejected,
                    GatewayTopUpErrorCodes.TopUpRequiresReview),
                TopUpStatus.Confirmed => Result.Conflict(
                    string.Format(DomainErrors.TopUpRequest_CannotRejectStatus, TopUpStatus.Confirmed),
                    GatewayTopUpErrorCodes.TopUpAlreadyConfirmed),
                null => Result.NotFound(string.Format(ApplicationErrors.TopUpRequest_NotFound,
                    request.ClientReferenceCode), GatewayTopUpErrorCodes.TopUpNotFound),
                _ => Result.Conflict(ApplicationErrors.TopUpRequest_IdempotencyConflict,
                    GatewayTopUpErrorCodes.TopUpConcurrentConfirmation)
            };
        }
    }
}
