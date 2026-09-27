using ByeMoney.API.Contracts.Settings;
using ByeMoney.API.Controllers;
using ByeMoney.Application.Modules.Settings;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ByeMoney.UnitTests;

public class SettingsControllerTests
{
    [Fact]
    public async Task GetConversionRate_UsesCurrentRialRateAndConvertsToToman()
    {
        var settings = new Mock<ISystemSettingRepository>();
        settings.Setup(x => x.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(12_345m);
        var controller = new SettingsController(settings.Object);

        var result = await controller.GetConversionRate(CancellationToken.None);

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<ConversionRateResponse>().Subject;
        response.RialPerNoor.Should().Be(12_345m);
        response.TomanPerNoor.Should().Be(1_234.5m);
        settings.Verify(x => x.GetRialToNoorConversionRateAsync(CancellationToken.None), Times.Once);
    }
}
