namespace ByeMoney.Domain.Common;

public abstract class BaseEntity<TId>
    where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    public DateTime CreatedAt { get; protected set; }

    public DateTime? UpdatedAt { get; protected set; }


    protected BaseEntity()
    {
        CreatedAt = DateTime.UtcNow;
    }
}