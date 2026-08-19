using ByeMoney.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace ByeMoney.Application.Common.Interfaces;

public interface IRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
    where TId : notnull
{
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default);
    Task<List<TEntity>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    void Update(TEntity entity);
    void Remove(TEntity entity);
}
