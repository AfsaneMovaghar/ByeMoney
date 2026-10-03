using ByeMoney.Application.Modules.Wallet.Commands.CancelGatewayTopUp;
using ByeMoney.Application.Modules.Wallet.Commands.ConfirmGatewayTopUp;
using ByeMoney.Application.Modules.Wallet.Commands.ConfirmTopUp;
using ByeMoney.Application.Modules.Wallet.Commands.ReportGatewayCancellation;
using ByeMoney.Application.Modules.Wallet.Constants;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Infrastructure.Modules.Wallet.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ByeMoney.UnitTests;

public class GatewayTopUpConcurrencyTests
{
    private static IServiceScopeFactory ScopeFactory(TopUpRequest topUp)
    {
        var repository = new Mock<ITopUpRequestRepository>();
        repository.Setup(x => x.GetByClientReferenceCodeAsync(topUp.ClientReferenceCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topUp);
        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(typeof(ITopUpRequestRepository))).Returns(repository.Object);
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(x => x.ServiceProvider).Returns(provider.Object);
        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(x => x.CreateScope()).Returns(scope.Object);
        return factory.Object;
    }

    [Fact]
    public async Task ConcurrentMatchingConfirmationIsIdempotent()
    {
        var topUp = TopUpRequest.CreateGateway(UserId.New(), 10m, 10_000m);
        topUp.ConfirmGateway("tx-1", "rrn-1", "SEP");
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<ConfirmTopUpCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());
        var handler = new ConfirmGatewayTopUpCommandHandler(sender.Object, ScopeFactory(topUp));

        var result = await handler.Handle(new ConfirmGatewayTopUpCommand(
            topUp.ClientReferenceCode, "SEP", "tx-1", "rrn-1", 100_000m, 100_000m), CancellationToken.None);

        result.Result.IsSuccess.Should().BeTrue();
        result.Idempotent.Should().BeTrue();
    }

    [Fact]
    public async Task ConcurrentConfirmationAfterCancellationRequiresReview()
    {
        var topUp = TopUpRequest.CreateGateway(UserId.New(), 10m, 10_000m);
        topUp.CancelGateway("SEP", "لغو پرداخت");
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<ConfirmTopUpCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());
        var handler = new ConfirmGatewayTopUpCommandHandler(sender.Object, ScopeFactory(topUp));

        var result = await handler.Handle(new ConfirmGatewayTopUpCommand(
            topUp.ClientReferenceCode, "SEP", "tx-1", "rrn-1", 100_000m, 100_000m), CancellationToken.None);

        result.Result.ErrorCode.Should().Be(GatewayTopUpErrorCodes.TopUpConcurrentConfirmation);
        result.Idempotent.Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentCancellationCannotUndoConfirmation()
    {
        var topUp = TopUpRequest.CreateGateway(UserId.New(), 10m, 10_000m);
        topUp.ConfirmGateway("tx-1", "rrn-1", "SEP");
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<CancelGatewayTopUpCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());
        var handler = new ReportGatewayCancellationCommandHandler(sender.Object, ScopeFactory(topUp));

        var result = await handler.Handle(new ReportGatewayCancellationCommand(topUp.ClientReferenceCode),
            CancellationToken.None);

        result.ErrorCode.Should().Be(GatewayTopUpErrorCodes.TopUpAlreadyConfirmed);
        topUp.Status.Should().Be(TopUpStatus.Confirmed);
    }
}
