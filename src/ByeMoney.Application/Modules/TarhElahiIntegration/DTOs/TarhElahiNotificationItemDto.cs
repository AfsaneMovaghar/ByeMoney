namespace ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;

public class TarhElahiNotificationItemDto
{
    public TarhElahiCatalogItemType Type { get; init; } = TarhElahiCatalogItemType.Course;
    public string ExternalId { get; init; } = default!;
    public string? ParentExternalId { get; init; }
}

