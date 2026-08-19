using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace ByeMoney.Infrastructure.Persistence;

public class BaseRepository<TEntity, TId> : IRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
    where TId : notnull
{
    protected readonly ApplicationDbContext Context;
    protected readonly DbSet<TEntity> DbSet;

    public BaseRepository(ApplicationDbContext context)
    {
        Context = context;
        DbSet = context.Set<TEntity>();
    }

    public async Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(e => e.Id.Equals(id), ct);

    public async Task<List<TEntity>> GetAllAsync(CancellationToken ct = default)
        => await DbSet.ToListAsync(ct);

    public async Task AddAsync(TEntity entity, CancellationToken ct = default)
        => await DbSet.AddAsync(entity, ct);

    public void Update(TEntity entity)
        => DbSet.Update(entity);

    public void Remove(TEntity entity)
        => DbSet.Remove(entity);
}
