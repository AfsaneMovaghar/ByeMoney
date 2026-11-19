using System.Security.Cryptography;
using System.Text;
using ByeMoney.Infrastructure.Modules.PostexIntegration;
using FluentAssertions;

namespace ByeMoney.UnitTests;

public sealed class PostexConfigFingerprintTests
{
    [Fact]
    public void LogsOnlyTheSha256OfNormalizedUrlAndKey()
    {
        const string apiKey = "super-secret-postex-key";
        var line = PostexConfigFingerprint.Describe("https://api.postex.ir///", apiKey);
        var expected = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"https://api.postex.ir|{apiKey}"))).ToLowerInvariant();

        line.Should().Be($"[Postex] config fingerprint: {expected}");
        line.Should().NotContain(apiKey);
        line.Should().NotContain("api.postex.ir");
    }

    [Fact]
    public void EmptyKeyIsReportedWithoutAFingerprint()
    {
        var line = PostexConfigFingerprint.Describe("https://api.postex.ir/", "   ");

        line.Should().Be("[Postex] API key is not configured.");
        line.Should().NotContain("api.postex.ir");
    }
}
