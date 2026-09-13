namespace ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;

public class CoursePurchaseNotificationDto
{
    public Guid EventId { get; init; }
    public Guid PurchaseId { get; init; }
    public string BuyerExternalUserId { get; init; } = default!;
    public TarhElahiNotificationItemDto Item { get; init; } = default!;
    public TarhElahiNotificationSnapshotDto Snapshot { get; init; } = default!;
    public DateTime PurchasedAtUtc { get; init; }
}

