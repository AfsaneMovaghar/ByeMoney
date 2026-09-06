using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using FluentAssertions;
using Xunit;

namespace ByeMoney.UnitTests;

public class TopUpRequestTests
{
    [Fact]
    public void Create_ShouldInitializeInPendingStatus()
    {
        // Arrange
        var userId = UserId.New();
        var amount = 50000m;

        // Act
        var request = TopUpRequest.Create(userId, amount, PaymentMethod.Gateway, "ext_123");

        // Assert
        request.Id.Value.Should().NotBeEmpty();
        request.UserId.Should().Be(userId);
        request.Amount.Should().Be(amount);
        request.PaymentMethod.Should().Be(PaymentMethod.Gateway);
        request.Status.Should().Be(TopUpStatus.Pending);
        request.ExternalTransactionId.Should().Be("ext_123");
        request.ConfirmedAtUtc.Should().BeNull();
        request.RejectedAtUtc.Should().BeNull();
        request.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Create_ShouldThrowDomainException_WhenAmountIsZeroOrNegative(decimal invalidAmount)
    {
        // Arrange
        var userId = UserId.New();

        // Act
        var act = () => TopUpRequest.Create(userId, invalidAmount, PaymentMethod.Gateway);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*بزرگتر از صفر*");
    }

    [Fact]
    public void Confirm_ShouldTransitionStatusToConfirmed_WhenPending()
    {
        // Arrange
        var request = TopUpRequest.Create(UserId.New(), 1000m, PaymentMethod.Gateway);

        // Act
        var result = request.Confirm("bank_ref_999");

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(TopUpStatus.Confirmed);
        request.ExternalTransactionId.Should().Be("bank_ref_999");
        request.ConfirmedAtUtc.Should().NotBeNull();
        request.ConfirmedAtUtc!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Confirm_ShouldReturnSuccess_WhenAlreadyConfirmedWithSameExternalTransactionId()
    {
        // Arrange
        var request = TopUpRequest.Create(UserId.New(), 1000m, PaymentMethod.Gateway);
        request.Confirm("ref_123");

        // Act
        var result = request.Confirm("ref_123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(TopUpStatus.Confirmed);
        request.ExternalTransactionId.Should().Be("ref_123");
    }

    [Fact]
    public void Confirm_ShouldReturnFailure_WhenAlreadyConfirmedWithDifferentExternalTransactionId()
    {
        // Arrange
        var request = TopUpRequest.Create(UserId.New(), 1000m, PaymentMethod.Gateway);
        request.Confirm("ref_123");

        // Act
        var result = request.Confirm("ref_456_different");

        // Assert
        result.IsFailure.Should().BeTrue();
        request.Status.Should().Be(TopUpStatus.Confirmed);
        request.ExternalTransactionId.Should().Be("ref_123");
    }

    [Fact]
    public void Confirm_ShouldReturnFailure_WhenStatusIsRejected()
    {
        // Arrange
        var request = TopUpRequest.Create(UserId.New(), 1000m, PaymentMethod.CardToCard);
        request.Reject("Payment not found");

        // Act
        var result = request.Confirm("bank_ref_999");

        // Assert
        result.IsFailure.Should().BeTrue();
        request.Status.Should().Be(TopUpStatus.Rejected);
    }

    [Fact]
    public void Reject_ShouldTransitionStatusToRejected()
    {
        // Arrange
        var request = TopUpRequest.Create(UserId.New(), 1000m, PaymentMethod.CardToCard);

        // Act
        request.Reject("Card not verified");

        // Assert
        request.Status.Should().Be(TopUpStatus.Rejected);
        request.RejectionReason.Should().Be("Card not verified");
        request.RejectedAtUtc.Should().NotBeNull();
        request.RejectedAtUtc!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Reject_ShouldThrowDomainException_WhenNotPending()
    {
        // Arrange
        var request = TopUpRequest.Create(UserId.New(), 1000m, PaymentMethod.CardToCard);
        request.Confirm("ref_123");

        // Act
        var act = () => request.Reject("Late rejection");

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*در انتظار*");
    }
}

