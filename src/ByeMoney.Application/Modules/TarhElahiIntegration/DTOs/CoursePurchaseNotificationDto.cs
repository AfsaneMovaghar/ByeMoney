using System.Text.Json.Serialization;

namespace ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;

public class CoursePurchaseNotificationDto
{
    public Guid EventId { get; init; }
    public Guid PurchaseId { get; init; }
    public string BuyerExternalUserId { get; init; } = default!;

    [JsonPropertyName("strapiUserId")]
    public string StrapiUserId => BuyerExternalUserId;

    public TarhElahiNotificationItemDto Item { get; init; } = default!;

    [JsonPropertyName("courseId")]
    public string CourseId => Item?.ExternalId ?? string.Empty;

    public TarhElahiNotificationSnapshotDto Snapshot { get; init; } = default!;
    public DateTime PurchasedAtUtc { get; init; }
}

