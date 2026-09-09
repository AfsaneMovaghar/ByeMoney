namespace ByeMoney.Infrastructure.Modules.TarhElahiIntegration.Configuration;

public class TarhElahiOptions
{
    public const string SectionName = "TarhElahi";

    public string BaseUrl { get; set; } = string.Empty;

    public string ServiceKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 10;
}

