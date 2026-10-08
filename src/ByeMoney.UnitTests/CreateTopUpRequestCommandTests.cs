using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Settings;
using ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using FluentAssertions;
using Moq;
using Xunit;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;
using ByeMoney.Domain.Common.Exceptions;

namespace ByeMoney.UnitTests;

public class CreateTopUpRequestCommandTests
{
    [Fact]
    public async Task Validator_RejectsNullPendingItem()
    {
        var result = await new CreateTopUpRequestCommandValidator().ValidateAsync(
            new CreateTopUpRequestCommand(Guid.NewGuid(), 50m, PaymentMethod.Gateway, PendingItems: [null!]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(x => x.PropertyName == "PendingItems[0]");
    }

    [Fact]
    public async Task Validator_RejectsUnsupportedPendingItemType()
    {
        var result = await new CreateTopUpRequestCommandValidator().ValidateAsync(new CreateTopUpRequestCommand(
            Guid.NewGuid(), 50m, PaymentMethod.Gateway, PendingItems: [new((PendingItemType)99, "course-1", 50m)]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "PendingItems[0].ItemType");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1.5)]
    public async Task InvalidCatalogPrice_DoesNotCreateTopUp(double price)
    {
        var repository = new Mock<ITopUpRequestRepository>();
        var catalog = new Mock<ITarhElahiIntegrationClient>();
        catalog.Setup(x => x.GetCourseAsync("course-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TarhElahiCourseDto { ExternalId = "course-1", PriceNoor = (decimal)price, Published = true, Available = true });
        var handler = new CreateTopUpRequestCommandHandler(repository.Object, Mock.Of<IUnitOfWork>(), Mock.Of<ISystemSettingRepository>(), catalog.Object);

        await FluentActions.Awaiting(() => handler.Handle(new CreateTopUpRequestCommand(Guid.NewGuid(), 50m,
            PaymentMethod.Gateway, PendingItems: [new(PendingItemType.Course, "course-1", 50m)]), CancellationToken.None))
            .Should().ThrowAsync<DomainException>();

        repository.Verify(x => x.AddAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(PaymentMethod.Gateway)]
    [InlineData(PaymentMethod.CardToCard)]
    public async Task PendingCourse_UsesCatalogPriceInsteadOfClientPrice(PaymentMethod method)
    {
        var repository = new Mock<ITopUpRequestRepository>();
        var settings = new Mock<ISystemSettingRepository>();
        settings.Setup(x => x.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(100m);
        var catalog = new Mock<ITarhElahiIntegrationClient>();
        catalog.Setup(x => x.GetCourseAsync("course-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TarhElahiCourseDto { ExternalId = "course-1", PriceNoor = 50m, Published = true, Available = true });
        TopUpRequest? saved = null;
        repository.Setup(x => x.AddAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TopUpRequest, CancellationToken>((request, _) => saved = request).Returns(Task.CompletedTask);
        var handler = new CreateTopUpRequestCommandHandler(repository.Object, Mock.Of<IUnitOfWork>(), settings.Object, catalog.Object);

        await handler.Handle(new CreateTopUpRequestCommand(Guid.NewGuid(), 50m, method,
            PendingItems: [new(PendingItemType.Course, "course-1", 1m)]), CancellationToken.None);

        saved!.PendingItems.Should().ContainSingle().Which.PriceNoorSnapshot.Should().Be(50m);
        catalog.Verify(x => x.GetCourseAsync("course-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnavailablePendingCourse_DoesNotCreateTopUp()
    {
        var repository = new Mock<ITopUpRequestRepository>();
        var catalog = new Mock<ITarhElahiIntegrationClient>();
        catalog.Setup(x => x.GetCourseAsync("course-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TarhElahiCourseDto { ExternalId = "course-1", PriceNoor = 50m, Published = true, Available = false });
        var handler = new CreateTopUpRequestCommandHandler(repository.Object, Mock.Of<IUnitOfWork>(), Mock.Of<ISystemSettingRepository>(), catalog.Object);

        await FluentActions.Awaiting(() => handler.Handle(new CreateTopUpRequestCommand(Guid.NewGuid(), 50m,
            PaymentMethod.Gateway, PendingItems: [new(PendingItemType.Course, "course-1", 1m)]), CancellationToken.None))
            .Should().ThrowAsync<DomainException>();

        repository.Verify(x => x.AddAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StandaloneTopUp_DoesNotRequireCatalog()
    {
        var catalog = new Mock<ITarhElahiIntegrationClient>(MockBehavior.Strict);
        var settings = new Mock<ISystemSettingRepository>();
        settings.Setup(x => x.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(100m);
        var handler = new CreateTopUpRequestCommandHandler(Mock.Of<ITopUpRequestRepository>(), Mock.Of<IUnitOfWork>(), settings.Object, catalog.Object);

        await handler.Handle(new CreateTopUpRequestCommand(Guid.NewGuid(), 50m, PaymentMethod.Gateway), CancellationToken.None);

        catalog.VerifyNoOtherCalls();
    }
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
        var handler = new CreateTopUpRequestCommandHandler(repository.Object, Mock.Of<IUnitOfWork>(), settings.Object, Mock.Of<ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces.ITarhElahiIntegrationClient>());

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
