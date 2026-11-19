using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;

namespace ByeMoney.Infrastructure.Modules.PostexIntegration;

public sealed class PostexOriginCityReader(PostexOptions options) : IShippingOriginCityReader
{
    public int? GetCityId(string? shippingOriginId)
    {
        var profile = options.ResolveOrigin(shippingOriginId);
        return profile is { CityId: >= 1 } ? profile.CityId : null;
    }
}
