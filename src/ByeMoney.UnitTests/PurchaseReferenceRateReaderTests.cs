using ByeMoney.Application.Modules.Settings.Interfaces;
using ByeMoney.Application.Modules.Purchases.Services;
using ByeMoney.Domain.Modules.Settings;
using FluentAssertions;
using Moq;

namespace ByeMoney.UnitTests;

public sealed class PurchaseReferenceRateReaderTests
{
    [Theory]
    [InlineData("999999999999999999")]
    [InlineData("137")]
    public async Task ValidRateIsFrozenWithoutChangingAfterSettingUpdate(string value)
    {
        var setting = SystemSetting.Create(SystemSettingConstants.Keys.RialToNoor, value);
        var settings = new Mock<ISystemSettingRepository>();
        settings.Setup(x => x.GetValueByKeyAsync(setting.Key, It.IsAny<CancellationToken>())).ReturnsAsync(() => setting.Value);

        var rate = await PurchaseReferenceRateReader.ReadAsync(settings.Object, default);
        var snapshot = Domain.Modules.Purchases.ProductSnapshot.Create(
            "course", "دوره", 10, referenceRialPerNoorAtPurchaseTime: rate);
        setting.UpdateValue("222");

        snapshot.ReferenceRialPerNoorAtPurchaseTime.Should().Be(decimal.Parse(value));
        (await PurchaseReferenceRateReader.ReadAsync(settings.Object, default)).Should().Be(222);
        snapshot.PriceInNoorAtPurchaseTime.Should().Be(10);
        settings.Verify(x => x.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DatabaseFailureIsNotConvertedToMissingRate()
    {
        var settings = new Mock<ISystemSettingRepository>();
        settings.Setup(x => x.GetValueByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());

        var read = () => PurchaseReferenceRateReader.ReadAsync(settings.Object, default);

        await read.Should().ThrowAsync<InvalidOperationException>();
    }
}
