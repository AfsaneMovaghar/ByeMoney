using Microsoft.EntityFrameworkCore;
using ByeMoney.Application.Common.Interfaces;

namespace ByeMoney.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IUnitOfWork   
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await base.SaveChangesAsync(ct);
}