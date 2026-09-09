using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Infrastructure.Modules.TarhElahiIntegration.Clients;
using ByeMoney.Infrastructure.Modules.TarhElahiIntegration.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ByeMoney.Infrastructure.Modules.TarhElahiIntegration;

public static class AddTarhElahiIntegrationModule
{
    public static IServiceCollection AddTarhElahiIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(TarhElahiOptions.SectionName)
            .Get<TarhElahiOptions>() ?? new TarhElahiOptions();

        // Fallback to BYEMONEY_SERVICE_KEY environment variable if ServiceKey is not configured in section
        if (string.IsNullOrWhiteSpace(options.ServiceKey))
        {
            var envServiceKey = configuration["BYEMONEY_SERVICE_KEY"];
            if (!string.IsNullOrWhiteSpace(envServiceKey))
            {
                options.ServiceKey = envServiceKey;
            }
        }

        services.AddHttpClient<ITarhElahiIntegrationClient, TarhElahiIntegrationClient>(client =>
        {
            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                var baseUrl = options.BaseUrl.TrimEnd('/') + "/";
                client.BaseAddress = new Uri(baseUrl);
            }

            if (!string.IsNullOrWhiteSpace(options.ServiceKey))
            {
                client.DefaultRequestHeaders.Add("X-Service-Key", options.ServiceKey);
            }

            var timeoutSeconds = options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 10;
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        });

        return services;
    }
}

