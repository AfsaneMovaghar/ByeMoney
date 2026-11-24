using ByeMoney.Application.Modules.Wallet.Withdrawals.Constants;
using ByeMoney.Application.Modules.Wallet.Withdrawals.DTOs;
using ByeMoney.Application.Modules.Wallet.Withdrawals.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Domain.Modules.Wallet.Withdrawals;
using ByeMoney.Infrastructure.Modules.Purchases.Persistence;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ByeMoney.Infrastructure.Modules.Wallet.Persistence.Withdrawals;

public sealed class NoorWithdrawalStore(ApplicationDbContext db) : INoorWithdrawalStore
{
    public async Task<Result<T>> ExecuteAsync<T>(UserId userId, Func<Task<Result<T>>> work, CancellationToken ct)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // همان قفل خریدارِ پرداخت سبد و خرید مستقیم؛ خروج و خرید هم‌زمان روی یک کیف پول سریال می‌شوند.
            await PurchaseAdvisoryLocks.LockBuyerAsync(db, userId.Value, ct);

            var result = await work();
            if (result.IsFailure)
            {
                db.ChangeTracker.Clear();
                return result;
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict<T>();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.CheckViolation
        })
        {
            return Conflict<T>();
        }
        catch (DomainException)
        {
            return Conflict<T>();
        }
    }

    public Task<NoorWithdrawal?> FindByOperationIdAsync(Guid operationId, CancellationToken ct) =>
        db.Set<NoorWithdrawal>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.OperationId == operationId, ct);

    public Task<NoorWithdrawal?> FindAsync(NoorWithdrawalId id, CancellationToken ct) =>
        db.Set<NoorWithdrawal>()
            .SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<UserId?> FindOwnerAsync(NoorWithdrawalId id, CancellationToken ct)
    {
        var owners = await db.Set<NoorWithdrawal>()
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => x.UserId)
            .ToListAsync(ct);

        return owners.Count == 0 ? null : owners[0];
    }

    public Task<bool> HasActiveRefundForTopUpAsync(TopUpRequestId topUpId, CancellationToken ct) =>
        db.Set<NoorWithdrawal>()
            .AsNoTracking()
            .AnyAsync(x => x.RelatedTopUpId == topUpId && x.Status != NoorWithdrawalStatus.Reversed, ct);

    public Task<bool> IsReceiptReferencedAsync(string receiptId, CancellationToken ct) =>
        db.Set<NoorWithdrawal>()
            .AsNoTracking()
            .AnyAsync(x => x.ReceiptId == receiptId, ct);

    public Task<string?> GetReceiptIdAsync(NoorWithdrawalId id, string scopeId, CancellationToken ct) =>
        db.Set<NoorWithdrawal>()
            .AsNoTracking()
            .Where(x => x.Id == id && x.ScopeId == scopeId)
            .Select(x => x.ReceiptId)
            .SingleOrDefaultAsync(ct);

    public void Add(NoorWithdrawal withdrawal) => db.Add(withdrawal);

    public async Task<NoorWithdrawalResponse?> GetResponseAsync(NoorWithdrawalId id, string scopeId, CancellationToken ct)
    {
        var withdrawals = await db.Set<NoorWithdrawal>()
            .AsNoTracking()
            .Where(x => x.Id == id && x.ScopeId == scopeId)
            .ToListAsync(ct);

        var responses = await ToResponsesAsync(withdrawals, ct);
        return responses.Count == 0 ? null : responses[0];
    }

    public async Task<IReadOnlyList<RefundableTopUpResponse>> ListRefundableTopUpsAsync(
        string userExternalId, int limit, string scopeId, CancellationToken ct)
    {
        var userIds = db.Set<User>()
            .Where(u => u.ExternalUserId == userExternalId)
            .Select(u => u.Id);

        var topUps = await db.Set<TopUpRequest>()
            .AsNoTracking()
            .Where(t => userIds.Contains(t.UserId) &&
                t.ScopeId == scopeId &&
                t.Status == TopUpStatus.Confirmed &&
                t.AmountRial != null &&
                t.RialPerNoorSnapshot != null)
            .OrderByDescending(t => t.ConfirmedAtUtc)
            .ThenByDescending(t => t.Id)
            .Take(limit)
            .ToListAsync(ct);

        var refundedIds = await db.Set<NoorWithdrawal>()
            .AsNoTracking()
            .Where(w => userIds.Contains(w.UserId) &&
                w.ScopeId == scopeId &&
                w.RelatedTopUpId != null &&
                w.Status != NoorWithdrawalStatus.Reversed)
            .Select(w => w.RelatedTopUpId!.Value)
            .ToListAsync(ct);

        return topUps
            .Select(t => new RefundableTopUpResponse(
                t.Id.Value,
                t.ClientReferenceCode,
                t.AmountNoor,
                t.AmountRial!.Value,
                t.RialPerNoorSnapshot!.Value,
                t.ConfirmedAtUtc,
                refundedIds.Contains(t.Id)))
            .ToList();
    }

    public async Task<IReadOnlyList<NoorWithdrawalResponse>> ListAsync(
        NoorWithdrawalStatus? status, string? userExternalId, int limit, string scopeId, CancellationToken ct)
    {
        var query = db.Set<NoorWithdrawal>().AsNoTracking().Where(x => x.ScopeId == scopeId);

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (userExternalId is not null)
        {
            var userIds = db.Set<User>()
                .Where(u => u.ExternalUserId == userExternalId)
                .Select(u => u.Id);
            query = query.Where(x => userIds.Contains(x.UserId));
        }

        var withdrawals = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(limit)
            .ToListAsync(ct);

        return await ToResponsesAsync(withdrawals, ct);
    }

    /// <summary>شناسهٔ داخلی کاربر و اجراکننده‌ها با یک کوئری به شناسهٔ استرپی ترجمه می‌شود.</summary>
    private async Task<IReadOnlyList<NoorWithdrawalResponse>> ToResponsesAsync(
        IReadOnlyList<NoorWithdrawal> withdrawals, CancellationToken ct)
    {
        if (withdrawals.Count == 0)
        {
            return [];
        }

        var userIds = withdrawals
            .SelectMany(x => new[] { x.UserId, x.CreatedByUserId, x.PaidByUserId, x.ReversedByUserId })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        var externalIds = await db.Set<User>()
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.ExternalUserId, ct);

        return withdrawals
            .Select(x => new NoorWithdrawalResponse(
                x.Id.Value,
                x.OperationId,
                externalIds[x.UserId],
                x.Kind.ToString(),
                x.Status.ToString(),
                x.RelatedTopUpId?.Value,
                x.AmountNoor,
                x.AmountRial,
                x.BeneficiaryName,
                x.BeneficiaryCardNumber,
                x.BeneficiaryIban,
                x.Description,
                externalIds[x.CreatedByUserId],
                x.CreatedAtUtc,
                x.PaymentReference,
                x.ReceiptId is not null,
                x.PaidByUserId.HasValue ? externalIds[x.PaidByUserId.Value] : null,
                x.PaidAtUtc,
                x.ReversalReason,
                x.ReversedByUserId.HasValue ? externalIds[x.ReversedByUserId.Value] : null,
                x.ReversedAtUtc))
            .ToList();
    }

    private Result<T> Conflict<T>()
    {
        db.ChangeTracker.Clear();
        return Result<T>.Conflict(NoorWithdrawalErrors.Conflict, NoorWithdrawalErrorCodes.Conflict);
    }
}
