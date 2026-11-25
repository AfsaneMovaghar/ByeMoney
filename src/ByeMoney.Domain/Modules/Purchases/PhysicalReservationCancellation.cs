using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Users;

namespace ByeMoney.Domain.Modules.Purchases;

/// <summary>
/// سابقهٔ لغو سبد پرداخت‌نشدهٔ دارای کالای فیزیکی برای تضمین یکتایی و تطابق بازپخش operationId.
/// یک سبد می‌تواند در چرخه‌های مختلف بازگشایی و لغو چند ردیف داشته باشد، اما هر operationId یکتا است.
/// </summary>
public class PhysicalReservationCancellation : BaseEntity<PhysicalReservationCancellationId>
{
    public Guid OperationId { get; private set; }
    public Guid CartId { get; private set; }
    public Guid ReservationId { get; private set; }
    public UserId BuyerId { get; private set; }
    public string Reason { get; private set; } = null!;

    private PhysicalReservationCancellation() { }

    public static PhysicalReservationCancellation Create(
        Guid operationId,
        Guid cartId,
        Guid reservationId,
        UserId buyerId,
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (operationId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(operationId));
        }

        if (cartId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(cartId));
        }

        if (reservationId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(reservationId));
        }

        var now = DateTime.UtcNow;

        return new PhysicalReservationCancellation
        {
            Id = PhysicalReservationCancellationId.New(),
            CreatedAtUtc = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond)),
            OperationId = operationId,
            CartId = cartId,
            ReservationId = reservationId,
            BuyerId = buyerId,
            Reason = reason.Trim()
        };
    }
}
