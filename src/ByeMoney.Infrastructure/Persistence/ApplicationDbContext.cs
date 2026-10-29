using Microsoft.EntityFrameworkCore;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using ByeMoney.Domain.Modules.Wallet.Gifts;

namespace ByeMoney.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IUnitOfWork   
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<GiftGrantBatch> GiftGrantBatches => Set<GiftGrantBatch>();
    public DbSet<GiftGrantRecipient> GiftGrantRecipients => Set<GiftGrantRecipient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await base.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < 3 &&
                ex.Entries.Count > 0 && ex.Entries.All(entry => entry.Entity is Wallet))
            {
                // تغییر مانده روی آخرین مقدار دوباره اعمال می‌شود؛ تمام ثبت‌های مالی با هم ذخیره می‌شوند.
                foreach (var entry in ex.Entries)
                {
                    var wallet = (Wallet)entry.Entity;
                    var delta = wallet.RegularBalance - entry.OriginalValues.GetValue<decimal>(nameof(Wallet.RegularBalance));
                    var giftDelta = wallet.GiftBalance - entry.OriginalValues.GetValue<decimal>(nameof(Wallet.GiftBalance));

                    // حفاظ دوم: تلاش مجدد با دلتای ایستا صرفاً برای افزایش مانده (Credit) جابجایی‌پذیر و ایمن است.
                    // در کسر مانده (Debit)، سهم‌بندی هدیه و عادی نیازمند ارزیابی مجدد بر اساس مانده تازه است و نباید بی‌صدا اعمال شود.
                    if (delta < 0 || giftDelta < 0)
                    {
                        throw;
                    }

                    var current = await entry.GetDatabaseValuesAsync(ct);
                    if (current is null) throw;
                    entry.OriginalValues.SetValues(current);
                    entry.CurrentValues.SetValues(current);
                    wallet.ApplyConcurrencyDelta(delta, giftDelta);
                }
            }
        }
    }
}

