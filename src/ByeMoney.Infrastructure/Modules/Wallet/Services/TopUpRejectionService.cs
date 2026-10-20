using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ByeMoney.Infrastructure.Modules.Wallet.Services;

public sealed class TopUpRejectionService(ITopUpRequestRepository repository, IUnitOfWork work,
    IServiceScopeFactory scopes) : ITopUpRejectionService
{
    public async Task<Result> RejectAsync(Guid topUpRequestId, string reason, CancellationToken ct = default)
    {
        var topUp = await repository.GetByIdAsync(new TopUpRequestId(topUpRequestId), ct);
        if (topUp is null)
            return Result.NotFound(string.Format(ApplicationErrors.TopUpRequest_NotFound, topUpRequestId));
        topUp.Reject(reason);
        repository.Update(topUp);
        try
        {
            await work.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            using var scope = scopes.CreateScope();
            var freshRepository = scope.ServiceProvider.GetRequiredService<ITopUpRequestRepository>();
            var freshWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var fresh = await freshRepository.GetByIdAsync(new TopUpRequestId(topUpRequestId), ct);
            if (fresh is null)
                return Result.NotFound(string.Format(ApplicationErrors.TopUpRequest_NotFound, topUpRequestId));
            if (fresh.Status != TopUpStatus.Pending)
                return Result.Conflict(ApplicationErrors.TopUpRequest_IdempotencyConflict);
            fresh.Reject(reason);
            freshRepository.Update(fresh);
            try
            {
                await freshWork.SaveChangesAsync(ct);
                return Result.Success();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Conflict(ApplicationErrors.TopUpRequest_IdempotencyConflict);
            }
        }
    }
}
