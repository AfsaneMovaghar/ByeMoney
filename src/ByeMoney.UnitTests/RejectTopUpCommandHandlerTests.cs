using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.RejectTopUp;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using FluentAssertions;
using Moq;
using Xunit;

namespace ByeMoney.UnitTests;

public class RejectTopUpCommandHandlerTests
{
    private readonly Mock<ITopUpRequestRepository> _topUpRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly RejectTopUpCommandHandler _handler;

    public RejectTopUpCommandHandlerTests()
    {
        _topUpRepoMock = new Mock<ITopUpRequestRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new RejectTopUpCommandHandler(
            _topUpRepoMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldRejectPendingRequest_WithCorrectStateTransition()
    {
        // Arrange
        var userId = UserId.New();
        var topUp = TopUpRequest.Create(userId, 100_000m, PaymentMethod.CardToCard);
        var command = new RejectTopUpCommand(topUp.Id.Value, "رسید بانکی نامعتبر");

        _topUpRepoMock
            .Setup(r => r.GetByIdAsync(topUp.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topUp);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        topUp.Status.Should().Be(TopUpStatus.Rejected);
        topUp.RejectionReason.Should().Be("رسید بانکی نامعتبر");
        topUp.RejectedAtUtc.Should().NotBeNull();
        topUp.RejectedAtUtc!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        _topUpRepoMock.Verify(r => r.Update(topUp), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotWriteAnyLedgerEntry_WhenRejectingPendingRequest()
    {
        // Arrange
        var userId = UserId.New();
        var topUp = TopUpRequest.Create(userId, 50_000m, PaymentMethod.CardToCard);
        var command = new RejectTopUpCommand(topUp.Id.Value, "Payment not received");

        _topUpRepoMock
            .Setup(r => r.GetByIdAsync(topUp.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topUp);

        // Note: We do NOT set up any IRepository<LedgerEntry, LedgerEntryId> mock
        // because the handler should NOT interact with ledger at all.

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        topUp.Status.Should().Be(TopUpStatus.Rejected);

        // Verify no ledger-related calls were made
        // (The handler doesn't even have a ledger repository dependency,
        //  which is the key difference from ConfirmTopUpCommandHandler)
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenTopUpRequestDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var command = new RejectTopUpCommand(nonExistentId, "Some reason");

        _topUpRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<TopUpRequestId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TopUpRequest?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.NotFound);
        _topUpRepoMock.Verify(r => r.Update(It.IsAny<TopUpRequest>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowDomainException_WhenTopUpRequestIsAlreadyConfirmed()
    {
        // Arrange
        var userId = UserId.New();
        var topUp = TopUpRequest.Create(userId, 100_000m, PaymentMethod.CardToCard);
        topUp.Confirm("bank_ref_123");

        var command = new RejectTopUpCommand(topUp.Id.Value, "Late rejection attempt");

        _topUpRepoMock
            .Setup(r => r.GetByIdAsync(topUp.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topUp);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainException>();
        topUp.Status.Should().Be(TopUpStatus.Confirmed);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowDomainException_WhenTopUpRequestIsAlreadyRejected()
    {
        // Arrange
        var userId = UserId.New();
        var topUp = TopUpRequest.Create(userId, 100_000m, PaymentMethod.CardToCard);
        topUp.Reject("First rejection");

        var command = new RejectTopUpCommand(topUp.Id.Value, "Second rejection attempt");

        _topUpRepoMock
            .Setup(r => r.GetByIdAsync(topUp.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topUp);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainException>();
        topUp.Status.Should().Be(TopUpStatus.Rejected);
        topUp.RejectionReason.Should().Be("First rejection");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
