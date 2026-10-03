using ByeMoney.Application.Modules.Wallet.Commands.ConfirmGatewayTopUp;
using ByeMoney.Application.Modules.Wallet.Commands.ConfirmTopUp;
using ByeMoney.Application.Modules.Wallet.Constants;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ByeMoney.Infrastructure.Modules.Wallet.Services;

public sealed class ConfirmGatewayTopUpCommandHandler(ISender sender, IServiceScopeFactory scopes)
    : IRequestHandler<ConfirmGatewayTopUpCommand, GatewayConfirmationOutcome>
{
    public async Task<GatewayConfirmationOutcome> Handle(ConfirmGatewayTopUpCommand request, CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(new ConfirmTopUpCommand(
                new TopUpRequestId(Guid.Empty), request.ExternalTransactionId, 0m,
                request.ClientReferenceCode, request.Gateway, request.BankReferenceNumber,
                request.OriginalAmountRial, request.AffectiveAmountRial), ct);
            return new GatewayConfirmationOutcome(result);
        }
        catch (DbUpdateConcurrencyException)
        {
            using var scope = scopes.CreateScope();
            var fresh = await scope.ServiceProvider.GetRequiredService<ITopUpRequestRepository>()
                .GetByClientReferenceCodeAsync(request.ClientReferenceCode, ct);
            if (fresh is { Status: TopUpStatus.Confirmed } &&
                fresh.ExternalTransactionId == request.ExternalTransactionId &&
                fresh.BankReferenceNumber == request.BankReferenceNumber &&
                fresh.GatewayName == request.Gateway &&
                fresh.AmountRial == request.OriginalAmountRial &&
                fresh.AmountRial == request.AffectiveAmountRial)
                return new GatewayConfirmationOutcome(Result.Success(), true);

            return new GatewayConfirmationOutcome(Result.Conflict(
                ApplicationErrors.TopUpRequest_IdempotencyConflict,
                GatewayTopUpErrorCodes.TopUpConcurrentConfirmation));
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_TopUpRequests_ExternalTransactionId" })
        {
            return new GatewayConfirmationOutcome(Result.Conflict(
                ApplicationErrors.TopUpRequest_IdempotencyConflict,
                GatewayTopUpErrorCodes.RefNumConflict));
        }
    }
}
