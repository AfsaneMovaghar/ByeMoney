using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;

public class TarhElahiNotificationSnapshotDto
{
    public string ProductSource { get; init; } = ProductCatalogSources.TarhElahi;
    public string ProductTitle { get; init; } = default!;
    public decimal PriceNoor { get; init; }
}

