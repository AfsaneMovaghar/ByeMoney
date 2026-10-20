using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using System.Data;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByeMoney.Infrastructure.Modules.Purchases.Persistence;

public sealed class CourseOrderReader(ApplicationDbContext db) : ICourseOrderReader
{
    public async Task<CourseOrderSnapshot> ReadAsync(
        Guid? buyerId, string? buyerExternalUserId, CancellationToken ct)
    {
        // تصویر ثابت مانع مشاهدهٔ نیمهٔ قبل و بعد از commit هم‌زمان خرید می‌شود.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var users = db.Set<User>().AsNoTracking();
        if (buyerId.HasValue)
        {
            var id = new UserId(buyerId.Value);
            users = users.Where(user => user.Id == id);
        }
        if (buyerExternalUserId is not null)
        {
            users = users.Where(user => user.ExternalUserId == buyerExternalUserId);
        }

        var buyers = await users.Select(user => new CourseOrderBuyer(
            user.Id.Value, user.ExternalUserId, (user.FirstName + " " + user.LastName).Trim())).ToListAsync(ct);
        var userIds = users.Select(user => user.Id);
        var cartOwners = buyers.Select(buyer => buyer.Id).ToArray();
        var carts = await db.Set<CourseCart>().AsNoTracking()
            .Where(cart => cartOwners.Contains(cart.BuyerId)).AsSplitQuery().ToListAsync(ct);
        var topUps = await db.Set<TopUpRequest>().AsNoTracking()
            .Where(topUp => userIds.Contains(topUp.UserId)).ToListAsync(ct);
        var purchases = await db.Set<CoursePurchase>().AsNoTracking()
            .Where(purchase => userIds.Contains(purchase.BuyerId)).ToListAsync(ct);
        await transaction.CommitAsync(ct);
        return new CourseOrderSnapshot(carts, topUps, purchases, buyers);
    }
}
