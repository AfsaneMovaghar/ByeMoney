using Microsoft.EntityFrameworkCore;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Domain.Modules.Wallet.Wallets;

namespace ByeMoney.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IUnitOfWork   
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

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
                    var delta = wallet.Balance - entry.OriginalValues.GetValue<decimal>(nameof(Wallet.Balance));
                    var current = await entry.GetDatabaseValuesAsync(ct);
                    if (current is null) throw;
                    entry.OriginalValues.SetValues(current);
                    entry.CurrentValues.SetValues(current);
                    if (delta > 0) wallet.ApplyCredit(delta);
                    else if (delta < 0) wallet.ApplyDebit(-delta);
                }
            }
        }
    }
}
