using ByeMoney.Infrastructure.Modules.Wallet.Services;
using FluentAssertions;

namespace ByeMoney.UnitTests;

public class PrivateReceiptStorageTests
{
    [Fact]
    public async Task SaveAsync_DistinguishesDifferentImagesWithTheSameIdempotencyKey()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ByeMoneyReceiptTests", Guid.NewGuid().ToString("N"));
        var storage = new PrivateReceiptStorage(folder);
        var firstImage = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 1 };
        var secondImage = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 2 };
        string? firstId = null;
        string? secondId = null;

        try
        {
            firstId = await storage.SaveAsync(new MemoryStream(firstImage), "receipt.png", "same-key");
            secondId = await storage.SaveAsync(new MemoryStream(secondImage), "receipt.png", "same-key");
            var repeatedId = await storage.SaveAsync(new MemoryStream(firstImage), "receipt.png", "same-key");

            firstId.Should().NotBe(secondId);
            repeatedId.Should().Be(firstId);
            (await storage.ExistsAsync(firstId)).Should().BeTrue();
            (await storage.ExistsAsync(secondId)).Should().BeTrue();
        }
        finally
        {
            if (firstId is not null) await storage.DeleteAsync(firstId);
            if (secondId is not null) await storage.DeleteAsync(secondId);
            if (Directory.Exists(folder)) Directory.Delete(folder);
        }
    }
}
