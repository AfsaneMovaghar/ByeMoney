using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Settings;
using ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using FluentAssertions;
using Moq;
using Xunit;

namespace ByeMoney.UnitTests;

public class CreateTopUpRequestCommandTests
{
    [Fact]
    public async Task GatewayTopUp_FreezesCurrentRateAndRialAmount()
    {
        var repository = new Mock<ITopUpRequestRepository>();
        var settings = new Mock<ISystemSettingRepository>();
        settings.Setup(x => x.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(137m);
        TopUpRequest? saved = null;
        repository.Setup(x => x.AddAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TopUpRequest, CancellationToken>((topUp, _) => saved = topUp)
            .Returns(Task.CompletedTask);
        var handler = new CreateTopUpRequestCommandHandler(repository.Object, Mock.Of<IUnitOfWork>(), settings.Object);

        await handler.Handle(new CreateTopUpRequestCommand(Guid.NewGuid(), 3m, PaymentMethod.Gateway), CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.AmountNoor.Should().Be(3m);
        saved.AmountRial.Should().Be(411m);
        saved.RialPerNoorSnapshot.Should().Be(137m);
        settings.Verify(x => x.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Validator_RejectsFractionalGatewayAmount()
    {
        var validator = new CreateTopUpRequestCommandValidator();

        var result = await validator.ValidateAsync(
            new CreateTopUpRequestCommand(Guid.NewGuid(), 1.5m, PaymentMethod.Gateway));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(x => x.PropertyName == "AmountNoor");
    }
}
