using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Infrastructure.Modules.PostexIntegration.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ByeMoney.Infrastructure.Modules.PostexIntegration;

public static class AddPostexIntegrationModule
{
    public const string HttpClientName = "Postex";

    public static IServiceCollection AddPostexIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(PostexOptions.SectionName)
            .Get<PostexOptions>() ?? new PostexOptions();

        // کلید و کلید خاموش/روشن از محیط هم قابل تنظیم است تا در فایل کانفیگ ذخیره نشود.
        if (bool.TryParse(configuration["POSTEX_ENABLED"], out var enabled))
        {
            options.Enabled = enabled;
        }

        var baseUrl = configuration["POSTEX_BASE_URL"];
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            options.BaseUrl = baseUrl;
        }

        var apiKey = configuration["POSTEX_API_KEY"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            options.ApiKey = apiKey;
        }

        options.TimeoutSeconds = options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 20;
        // بایندر کانفیگ comparer را حفظ نمی‌کند؛ برای شناسهٔ مبدأ مقایسه بدون حساسیت به حروف لازم است.
        options.Origins = new Dictionary<string, PostexSenderOptions>(
            options.Origins ?? new Dictionary<string, PostexSenderOptions>(),
            StringComparer.OrdinalIgnoreCase);
        options.EnsureLegacySenderMapped();

        services.AddSingleton(options);
        services.AddSingleton<IShippingOriginCityReader, PostexOriginCityReader>();
        services.AddHostedService<PostexConfigFingerprintLogger>();

        services.AddHttpClient(HttpClientName, client =>
        {
            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                client.BaseAddress = new Uri(options.BaseUrl.Trim().TrimEnd('/') + "/");
            }

            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                client.DefaultRequestHeaders.Remove("x-api-key");
                client.DefaultRequestHeaders.Add("x-api-key", options.ApiKey.Trim());
            }

            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        // هر قابلیت (استعلام / ساخت) کلاینت جدا با همان HttpClient نام‌دار؛
        // واسط بعدی فقط پیاده‌سازی‌های ITrackingProviderClient و/یا IShipmentProviderClient خودش را ثبت می‌کند.
        services.AddTransient<ITrackingProviderClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new PostexTrackingClient(factory.CreateClient(HttpClientName), sp.GetRequiredService<PostexOptions>());
        });
        services.AddTransient<IShipmentProviderClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new PostexShipmentClient(
                factory.CreateClient(HttpClientName),
                sp.GetRequiredService<PostexOptions>(),
                sp.GetRequiredService<IDeliveryCityCodeReader>());
        });

        return services;
    }
}
