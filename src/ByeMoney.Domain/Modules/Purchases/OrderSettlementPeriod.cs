using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Users;

namespace ByeMoney.Domain.Modules.Purchases;

/// <summary>
/// دورهٔ تسویهٔ دستی سفارش‌ها بین ادمین‌ها (D10). فقط تگ گزارشی است و هیچ ثبت Ledger یا تغییر مانده‌ای ندارد.
/// تعداد و جمع نور خالص در لحظهٔ بستن ثابت می‌شوند؛ <c>CreatedAtUtc</c> زمان بستن است.
/// </summary>
public class OrderSettlementPeriod : BaseEntity<OrderSettlementPeriodId>
{
    public const int TitleMaxLength = 200;
    public const int NoteMaxLength = 1000;

    private readonly List<OrderSettlementItem> _items = [];

    public string? Title { get; private set; }
    public string? Note { get; private set; }
    public int OrderCount { get; private set; }
    public decimal TotalRegularNetAmountInNoor { get; private set; }
    public decimal TotalGiftNetAmountInNoor { get; private set; }

    /// <summary>جمع کل نور خالص دوره؛ همیشه برابر جمع سهم عادی به‌علاوهٔ جمع سهم هدیه.</summary>
    public decimal TotalNetAmountInNoor { get; private set; }

    public UserId ClosedByUserId { get; private set; }
    public IReadOnlyCollection<OrderSettlementItem> Items => _items;

    private OrderSettlementPeriod() { }

    public static OrderSettlementPeriod Close(
        UserId closedByUserId,
        string? title,
        string? note,
        IReadOnlyList<OrderSettlementItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            throw new ArgumentException("A settlement period needs at least one order.", nameof(items));
        }

        if (items.Select(item => item.OrderId).Distinct(StringComparer.Ordinal).Count() != items.Count)
        {
            throw new ArgumentException("A settlement period cannot contain an order twice.", nameof(items));
        }

        var now = DateTime.UtcNow;
        var period = new OrderSettlementPeriod
        {
            Id = OrderSettlementPeriodId.New(),
            // دقت timestamp در PostgreSQL میکروثانیه است؛ پاسخ بستن با مقدار ذخیره‌شده یکی بماند.
            CreatedAtUtc = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond)),
            Title = Normalize(title),
            Note = Normalize(note),
            OrderCount = items.Count,
            TotalRegularNetAmountInNoor = items.Sum(item => item.RegularNetAmountInNoor),
            TotalGiftNetAmountInNoor = items.Sum(item => item.GiftNetAmountInNoor),
            TotalNetAmountInNoor = items.Sum(item => item.NetAmountInNoor),
            ClosedByUserId = closedByUserId
        };

        foreach (var item in items)
        {
            item.AttachTo(period.Id);
            period._items.Add(item);
        }

        return period;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
