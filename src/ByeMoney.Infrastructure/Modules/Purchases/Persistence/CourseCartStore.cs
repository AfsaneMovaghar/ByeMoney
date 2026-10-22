using ByeMoney.Application.Modules.Purchases.Cart.Constants;
using ByeMoney.Application.Modules.Purchases.Cart.Interfaces;
using ByeMoney.Application.Modules.Purchases.Cart.Services;
using ByeMoney.Application.Modules.Settings.Interfaces;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ByeMoney.Infrastructure.Modules.Purchases.Persistence;

public sealed class CourseCartStore(IServiceScopeFactory scopes) : ICourseCartStore
{
    private async Task<T> Run<T>(Func<IServiceProvider, ApplicationDbContext, Task<T>> action)
    {
        using var scope = scopes.CreateScope();
        return await action(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private static Task Lock(ApplicationDbContext db, Guid id, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(1869767797, hashtext({id.ToString()}))", ct);

    private static Task LockBuyer(ApplicationDbContext db, Guid id, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(1869767798, hashtext({id.ToString()}))", ct);

    public Task<CourseCart?> FindAsync(Guid id, Guid buyerId, CancellationToken ct) =>
        Run((_, db) => db.Set<CourseCart>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.BuyerId == buyerId, ct));

    public Task<CourseCart?> FindByTopUpAsync(Guid topUpId, Guid buyerId, CancellationToken ct) =>
        Run((_, db) => db.Set<CourseCart>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => (x.TopUpRequestId == topUpId || x.TopUpRequestIds.Contains(topUpId)) && x.BuyerId == buyerId, ct));

    public Task<Result<CourseCart>> CreateAsync(CourseCart cart, CancellationToken ct) => Run(async (_, db) =>
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Lock(db, cart.Id, ct);

        var existing = await db.Set<CourseCart>().FindAsync([cart.Id], ct);
        if (existing is not null)
        {
            return existing.BuyerId == cart.BuyerId && existing.Matches(cart.ExternalCourseIds, cart.CouponCode)
                ? Result<CourseCart>.Success(existing)
                : Result<CourseCart>.Conflict(CartErrors.Conflict, CartErrorCodes.Conflict);
        }

        db.Add(cart);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result<CourseCart>.Success(cart);
    });

    public Task<Result<CourseCart>> CancelAsync(Guid id, Guid buyerId, CancellationToken ct) => Run(async (_, db) =>
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Lock(db, id, ct);

        var cart = await db.Set<CourseCart>().SingleOrDefaultAsync(x => x.Id == id && x.BuyerId == buyerId, ct);
        if (cart is null)
        {
            return Result<CourseCart>.NotFound(CartErrors.NotFound, CartErrorCodes.NotFound);
        }

        if (cart.Status is CourseCartStatus.Preparing or CourseCartStatus.Ready)
        {
            cart.Fail();
            await db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
        return Result<CourseCart>.Success(cart);
    });

    public Task<Result<CourseCart>> ReopenAsync(Guid id, Guid buyerId, Guid reservationId, CancellationToken ct) => Run(async (_, db) =>
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockBuyer(db, buyerId, ct);
        await Lock(db, id, ct);

        var cart = await db.Set<CourseCart>().SingleAsync(x => x.Id == id && x.BuyerId == buyerId, ct);
        if (cart.ReservationId != reservationId)
        {
            return Result<CourseCart>.Success(cart);
        }

        if (cart.Status is not (CourseCartStatus.Failed or CourseCartStatus.Expired) || !cart.Synchronized)
        {
            return Conflict();
        }

        if (cart.TopUpRequestId is Guid topUpId)
        {
            var topUp = await db.Set<TopUpRequest>().FindAsync([new TopUpRequestId(topUpId)], ct);
            if (topUp?.Status is not (TopUpStatus.Rejected or TopUpStatus.Confirmed))
            {
                return Conflict();
            }
        }

        cart.Reopen();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result<CourseCart>.Success(cart);
    });

    public Task<CourseCart> SetQuoteAsync(Guid id, Guid buyerId, IReadOnlyList<CourseCartQuoteItem> items, Guid reservationId, CancellationToken ct) =>
        SetQuoteAsync(id, buyerId, items, 0m, null, reservationId, ct);

    public Task<CourseCart> SetQuoteAsync(Guid id, Guid buyerId, IReadOnlyList<CourseCartQuoteItem> items, decimal shippingAmountInNoor, string? shippingRuleLabel, Guid reservationId, CancellationToken ct) => Run(async (_, db) =>
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Lock(db, id, ct);

        var cart = await db.Set<CourseCart>().SingleAsync(x => x.Id == id && x.BuyerId == buyerId, ct);
        if (cart.ReservationId == reservationId)
        {
            cart.SetQuote(items, shippingAmountInNoor, shippingRuleLabel);
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return cart;
    });

    public async Task<Result<CourseCart>> PayAsync(
        Guid id,
        Guid buyerId,
        Guid reservationId,
        CancellationToken ct,
        decimal? referenceRate = null)
    {
        try
        {
            return await Run(async (sp, db) =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                await LockBuyer(db, buyerId, ct);
                await Lock(db, id, ct);

                var cart = await db.Set<CourseCart>().SingleAsync(x => x.Id == id && x.BuyerId == buyerId, ct);
                if (cart.Status == CourseCartStatus.Paid)
                {
                    return Result<CourseCart>.Success(cart);
                }

                if (cart.ReservationId != reservationId)
                {
                    return Conflict();
                }

                if (cart.Status is not (CourseCartStatus.Ready or CourseCartStatus.AwaitingTopUp))
                {
                    return Conflict();
                }

                if (cart.Status == CourseCartStatus.Ready && cart.ExpiresAtUtc <= DateTime.UtcNow)
                {
                    return Conflict();
                }

                if (cart.TopUpRequestId is Guid topUpId)
                {
                    var topUp = await db.Set<TopUpRequest>().FindAsync([new TopUpRequestId(topUpId)], ct);
                    if (topUp?.Status != TopUpStatus.Confirmed)
                    {
                        return Conflict();
                    }
                }

                var buyer = new UserId(buyerId);
                var active = await db.Set<User>().AnyAsync(x => x.Id == buyer && x.IsActive, ct);
                if (!active)
                {
                    return Conflict();
                }

                var productIds = cart.Items
                    .Where(x => x.ItemType != ProductItemType.Product)
                    .Select(x => x.ExternalCourseId)
                    .ToArray();
                var parentIds = cart.Items
                    .Where(x => x.ItemType == ProductItemType.CourseChapter && !string.IsNullOrEmpty(x.ParentExternalId))
                    .Select(x => x.ParentExternalId!)
                    .Distinct()
                    .ToArray();

                var purchaseQuery = db.Set<CoursePurchase>().Where(x => x.BuyerId == buyer);
                var alreadyPurchased = productIds.Length > 0 && (parentIds.Length > 0
                    ? await purchaseQuery.AnyAsync(x =>
                        productIds.Contains(x.Snapshot.ExternalProductId) ||
                        (x.Snapshot.ItemType == ProductItemType.Course && parentIds.Contains(x.Snapshot.ExternalProductId)), ct)
                    : await purchaseQuery.AnyAsync(x => productIds.Contains(x.Snapshot.ExternalProductId), ct));

                if (alreadyPurchased)
                {
                    return Conflict();
                }

                var provisioning = sp.GetRequiredService<IUserWalletProvisioningService>();
                var provisioned = await provisioning.GetOrCreateUserWalletAsync(buyer, ct);

                if (provisioned.Wallet.Balance < cart.Total)
                {
                    return Result<CourseCart>.Failure(CartErrors.InsufficientBalance, CartErrorCodes.InsufficientBalance);
                }

                var system = cart.Total > 0 ? await provisioning.GetOrCreateSystemAccountAsync(ct) : null;
                var payment = CourseCartPayment.Apply(cart, provisioned, system, referenceRate);
                db.AddRange(payment.Purchases);
                db.AddRange(payment.PhysicalPurchases);
                db.AddRange(payment.LedgerEntries);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return Result<CourseCart>.Success(cart);
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict();
        }
        catch (DomainException)
        {
            return Conflict();
        }
    }

    private static Result<CourseCart> Conflict() =>
        Result<CourseCart>.Conflict(CartErrors.Conflict, CartErrorCodes.Conflict);

    public Task<Result<CreateTopUpRequestResponse>> CreateTopUpAsync(Guid id, Guid buyerId, CancellationToken ct) => Run(async (sp, db) =>
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockBuyer(db, buyerId, ct);
        await Lock(db, id, ct);

        var cart = await db.Set<CourseCart>().SingleAsync(x => x.Id == id && x.BuyerId == buyerId, ct);
        TopUpRequest? topUp;

        if (cart.TopUpRequestId is Guid topUpId)
        {
            topUp = await db.Set<TopUpRequest>().FindAsync([new TopUpRequestId(topUpId)], ct);
        }
        else
        {
            if (cart.Status != CourseCartStatus.Ready || cart.ExpiresAtUtc <= DateTime.UtcNow)
            {
                return Result<CreateTopUpRequestResponse>.Conflict(CartErrors.Conflict, CartErrorCodes.Conflict);
            }

            var wallet = await sp.GetRequiredService<IWalletRepository>().GetByUserIdAsync(new UserId(buyerId), ct);
            var amount = decimal.Ceiling(cart.Total - (wallet?.Balance ?? 0m));
            if (amount <= 0)
            {
                return Result<CreateTopUpRequestResponse>.Conflict(CartErrors.Conflict, CartErrorCodes.Conflict);
            }

            var rate = await sp.GetRequiredService<ISystemSettingRepository>().GetRialToNoorConversionRateAsync(ct);
            if (rate <= 0 || decimal.Truncate(rate) != rate)
            {
                return Result<CreateTopUpRequestResponse>.Failure(CartErrors.Invalid, CartErrorCodes.Invalid);
            }

            topUp = TopUpRequest.CreateGateway(new UserId(buyerId), amount, rate);
            db.Add(topUp);
            cart.AwaitTopUp(topUp.Id.Value);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        return topUp is not null && topUp.Status == TopUpStatus.Pending
            ? Result<CreateTopUpRequestResponse>.Success(new(topUp.Id.Value, topUp.ClientReferenceCode, topUp.AmountRial!.Value))
            : Result<CreateTopUpRequestResponse>.Conflict(CartErrors.Conflict, CartErrorCodes.Conflict);
    });

    public Task FailAsync(Guid id, Guid buyerId, bool expired, Guid reservationId, CancellationToken ct) =>
        Mutate(id, buyerId, (cart, _) =>
        {
            if (cart.ReservationId == reservationId)
            {
                cart.Fail(expired);
            }
        }, ct);

    public Task RecordSynchronizationAsync(Guid id, Guid buyerId, bool succeeded, string? errorCode, Guid reservationId, CancellationToken ct) =>
        Mutate(id, buyerId, (cart, db) =>
        {
            if (cart.ReservationId != reservationId)
            {
                return;
            }

            cart.RecordSynchronization(succeeded, errorCode);
            if (!succeeded || cart.Status != CourseCartStatus.Paid)
            {
                return;
            }

            foreach (var purchase in db.Set<CoursePurchase>().Where(x => x.CartId == id).ToList())
            {
                if (purchase.Status != CoursePurchaseStatus.NotificationSent)
                {
                    purchase.MarkNotificationSent();
                }
            }
        }, ct);

    private Task Mutate(Guid id, Guid buyerId, Action<CourseCart, ApplicationDbContext> mutate, CancellationToken ct) => Run(async (_, db) =>
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Lock(db, id, ct);

        var cart = await db.Set<CourseCart>().SingleAsync(x => x.Id == id && x.BuyerId == buyerId, ct);
        mutate(cart, db);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    });

    public Task<IReadOnlyList<CourseCart>> GetPendingAsync(CancellationToken ct) => Run<IReadOnlyList<CourseCart>>(async (_, db) =>
    {
        var now = DateTime.UtcNow;
        var query =
            from c in db.Set<CourseCart>()
            join t in db.Set<TopUpRequest>()
                on EF.Property<Guid?>(c, nameof(CourseCart.TopUpRequestId))
                equals (Guid?)EF.Property<Guid>(t, nameof(TopUpRequest.Id)) into topUpGroup
            from t in topUpGroup.DefaultIfEmpty()
            where !c.Synchronized && (
                c.Status == CourseCartStatus.Paid ||
                c.Status == CourseCartStatus.Failed ||
                c.Status == CourseCartStatus.Expired ||
                ((c.Status == CourseCartStatus.Preparing || c.Status == CourseCartStatus.Ready) && c.ExpiresAtUtc <= now) ||
                (c.Status == CourseCartStatus.AwaitingTopUp && t != null && t.Status != TopUpStatus.Pending))
            orderby c.UpdatedAtUtc
            select c;

        return await query.Take(100).AsNoTracking().ToListAsync(ct);
    });
}

