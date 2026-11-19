using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ByeMoney.Infrastructure.Modules.PostexIntegration;

/// <summary>
/// اثرانگشت راه‌اندازی پستکس. فقط در لاگ استارتاپ می‌آید و روی هیچ مسیر عمومی نیست.
/// </summary>
public static class PostexConfigFingerprint
{
    public static string Describe(string? baseUrl, string? apiKey)
    {
        var key = apiKey?.Trim() ?? string.Empty;
        if (key.Length == 0)
        {
            return "[Postex] API key is not configured.";
        }

        var normalizedBaseUrl = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
        var fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(normalizedBaseUrl + "|" + key))).ToLowerInvariant();
        return $"[Postex] config fingerprint: {fingerprint}";
    }
}

public sealed class PostexConfigFingerprintLogger(
    PostexOptions options,
    ILogger<PostexConfigFingerprintLogger> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var line = PostexConfigFingerprint.Describe(options.BaseUrl, options.ApiKey);
        if (line.Contains("fingerprint:", StringComparison.Ordinal))
        {
            logger.LogInformation("{PostexConfig}", line);
        }
        else
        {
            logger.LogWarning("{PostexConfig}", line);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
