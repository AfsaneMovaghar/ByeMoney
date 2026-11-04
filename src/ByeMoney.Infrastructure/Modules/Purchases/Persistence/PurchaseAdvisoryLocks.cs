using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByeMoney.Infrastructure.Modules.Purchases.Persistence;

/// <summary>
/// قفل‌های تراکنشی مشترک مسیرهای مالی خرید. SQL خام لازم است، چون EF Core برای pg_advisory_xact_lock
/// معادل LINQ ندارد؛ ورودی فقط پارامتری ارسال می‌شود. ترتیب گرفتن قفل همیشه اول خریدار و سپس سبد است تا بن‌بست پیش نیاید.
/// </summary>
internal static class PurchaseAdvisoryLocks
{
    /// <summary>فضای کلید قفل سبد؛ مقدار ثابت تاریخی همان مسیر پرداخت سبد است و نباید تغییر کند.</summary>
    private const int CartLockSpace = 1869767797;

    /// <summary>فضای کلید قفل خریدار؛ مشترک با پرداخت سبد و خرید مستقیم دوره.</summary>
    private const int BuyerLockSpace = 1869767798;

    public static Task LockCartAsync(ApplicationDbContext db, Guid cartId, CancellationToken ct) =>
        db.Database.IsNpgsql()
            ? db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({CartLockSpace}, hashtext({cartId.ToString()}))", ct)
            : Task.CompletedTask;

    public static Task LockBuyerAsync(ApplicationDbContext db, Guid buyerId, CancellationToken ct) =>
        db.Database.IsNpgsql()
            ? db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({BuyerLockSpace}, hashtext({buyerId.ToString()}))", ct)
            : Task.CompletedTask;
}
