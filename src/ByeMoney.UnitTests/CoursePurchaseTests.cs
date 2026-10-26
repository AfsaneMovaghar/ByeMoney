using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using FluentAssertions;
using Xunit;

namespace ByeMoney.UnitTests;

public class CoursePurchaseTests
{
    private readonly UserId _buyerId = UserId.New();
    private readonly ProductSnapshot _validSnapshot = ProductSnapshot.Create(
        externalProductId: "course-doc-123",
        productTitle: "آموزش پیشرفته",
        priceNoor: 2500m,
        productSource: ProductCatalogSources.TarhElahi);

    [Fact]
    public void Create_ShouldInitializeInPendingState_WithZeroAttemptsAndCorrectSnapshot()
    {
        // Act
        var purchase = CoursePurchase.Create(_buyerId, _validSnapshot);

        // Assert
        purchase.Id.Value.Should().NotBeEmpty();
        purchase.BuyerId.Should().Be(_buyerId);
        purchase.Status.Should().Be(CoursePurchaseStatus.Pending);
        purchase.Snapshot.Should().Be(_validSnapshot);
        purchase.Snapshot.ExternalProductId.Should().Be("course-doc-123");
        purchase.Snapshot.ProductTitle.Should().Be("آموزش پیشرفته");
        purchase.Snapshot.PriceInRialAtPurchaseTime.Should().BeNull();
        purchase.Snapshot.ConversionRateAtPurchaseTime.Should().BeNull();
        purchase.Snapshot.PriceInNoorAtPurchaseTime.Should().Be(2500m);
        purchase.Snapshot.ProductSource.Should().Be(ProductCatalogSources.TarhElahi);
        purchase.LedgerTransactionId.Should().BeNull();
        purchase.NotificationAttempts.Should().Be(0);
        purchase.LastNotificationAttemptAtUtc.Should().BeNull();
        purchase.NotificationFailureReason.Should().BeNull();
        purchase.CompletedAtUtc.Should().BeNull();
    }

    [Fact]
    public void MarkDebited_ShouldTransitionToDebited_AndSetLedgerTransactionId_WhenPending()
    {
        // Arrange
        var purchase = CoursePurchase.Create(_buyerId, _validSnapshot);
        var transactionId = Guid.NewGuid();

        // Act
        purchase.MarkDebited(transactionId);

        // Assert
        purchase.Status.Should().Be(CoursePurchaseStatus.Debited);
        purchase.LedgerTransactionId.Should().Be(transactionId);
        purchase.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkDebited_ShouldThrowDomainException_WhenNotPending()
    {
        // Arrange
        var purchase = CoursePurchase.Create(_buyerId, _validSnapshot);
        purchase.MarkDebited(Guid.NewGuid());

        // Act
        var act = () => purchase.MarkDebited(Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkDebited_ShouldThrowDomainException_WhenTransactionIdIsEmpty()
    {
        // Arrange
        var purchase = CoursePurchase.Create(_buyerId, _validSnapshot);

        // Act
        var act = () => purchase.MarkDebited(Guid.Empty);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkNotificationSent_ShouldTransitionToNotificationSent_AndSetCompletedAtUtc_WhenDebited()
    {
        // Arrange
        var purchase = CoursePurchase.Create(_buyerId, _validSnapshot);
        purchase.MarkDebited(Guid.NewGuid());

        // Act
        purchase.MarkNotificationSent();

        // Assert
        purchase.Status.Should().Be(CoursePurchaseStatus.NotificationSent);
        purchase.NotificationAttempts.Should().Be(1);
        purchase.CompletedAtUtc.Should().NotBeNull();
        purchase.LastNotificationAttemptAtUtc.Should().NotBeNull();
        purchase.NotificationFailureReason.Should().BeNull();
    }

    [Fact]
    public void MarkNotificationSent_ShouldThrowDomainException_WhenPending()
    {
        // Arrange
        var purchase = CoursePurchase.Create(_buyerId, _validSnapshot);

        // Act
        var act = () => purchase.MarkNotificationSent();

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RecordNotificationFailure_ShouldTransitionToNotificationFailed_AndIncrementAttempts()
    {
        // Arrange
        var purchase = CoursePurchase.Create(_buyerId, _validSnapshot);
        purchase.MarkDebited(Guid.NewGuid());

        // Act
        purchase.RecordNotificationFailure("Network timeout");

        // Assert
        purchase.Status.Should().Be(CoursePurchaseStatus.NotificationFailed);
        purchase.NotificationAttempts.Should().Be(1);
        purchase.NotificationFailureReason.Should().Be("Network timeout");
        purchase.LastNotificationAttemptAtUtc.Should().NotBeNull();
        purchase.CompletedAtUtc.Should().BeNull();

        // Second failure
        purchase.RecordNotificationFailure("500 Internal Server Error");
        purchase.NotificationAttempts.Should().Be(2);
        purchase.NotificationFailureReason.Should().Be("500 Internal Server Error");
    }

    [Fact]
    public void RecordNotificationFailure_ShouldThrowDomainException_WhenPending()
    {
        // Arrange
        var purchase = CoursePurchase.Create(_buyerId, _validSnapshot);

        // Act
        var act = () => purchase.RecordNotificationFailure("Timeout");

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkFree_ShouldTransitionToDebited_AndSetLedgerTransactionId_WhenPendingAndFree()
    {
        // Arrange
        var freeSnapshot = ProductSnapshot.Create(
            externalProductId: "course-free-1",
            productTitle: "دوره رایگان",
            priceNoor: 0m,
            isFree: true);
        var purchase = CoursePurchase.Create(_buyerId, freeSnapshot, isFree: true, freeReason: "هدیه");
        var referenceId = Guid.NewGuid();

        // Act
        purchase.MarkFree(referenceId);

        // Assert
        purchase.Status.Should().Be(CoursePurchaseStatus.Debited);
        purchase.LedgerTransactionId.Should().Be(referenceId);
        purchase.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkFree_ShouldThrowDomainException_WhenNotFree()
    {
        // Arrange
        var purchase = CoursePurchase.Create(_buyerId, _validSnapshot, isFree: false);

        // Act
        var act = () => purchase.MarkFree(Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkFree_ShouldThrowDomainException_WhenNotPending()
    {
        // Arrange
        var freeSnapshot = ProductSnapshot.Create("free-1", "رایگان", 0m, isFree: true);
        var purchase = CoursePurchase.Create(_buyerId, freeSnapshot, isFree: true);
        purchase.MarkFree(Guid.NewGuid());

        // Act
        var act = () => purchase.MarkFree(Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkFree_ShouldThrowDomainException_WhenTransactionIdIsEmptyGuid()
    {
        // Arrange
        var freeSnapshot = ProductSnapshot.Create("free-1", "رایگان", 0m, isFree: true);
        var purchase = CoursePurchase.Create(_buyerId, freeSnapshot, isFree: true);

        // Act
        var act = () => purchase.MarkFree(Guid.Empty);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkNotificationSent_ShouldThrowDomainException_WhenPending_EvenIfFree()
    {
        // Arrange
        var freeSnapshot = ProductSnapshot.Create("free-1", "رایگان", 0m, isFree: true);
        var purchase = CoursePurchase.Create(_buyerId, freeSnapshot, isFree: true);

        // Act
        var act = () => purchase.MarkNotificationSent();

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkNotificationSent_ShouldSucceed_WhenFreeAndDebited()
    {
        // Arrange
        var freeSnapshot = ProductSnapshot.Create("free-1", "رایگان", 0m, isFree: true);
        var purchase = CoursePurchase.Create(_buyerId, freeSnapshot, isFree: true);
        purchase.MarkFree(Guid.NewGuid());

        // Act
        purchase.MarkNotificationSent();

        // Assert
        purchase.Status.Should().Be(CoursePurchaseStatus.NotificationSent);
        purchase.CompletedAtUtc.Should().NotBeNull();
    }
}
