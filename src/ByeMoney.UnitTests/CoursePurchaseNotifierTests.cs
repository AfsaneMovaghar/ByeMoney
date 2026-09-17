using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.Purchases.Services;
using ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ByeMoney.UnitTests;

public class CoursePurchaseNotifierTests
{
    private readonly Mock<ITarhElahiIntegrationClient> _tarhElahiClientMock = new();
    private readonly Mock<ICoursePurchaseRepository> _coursePurchaseRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private readonly CoursePurchaseNotifier _notifier;

    public CoursePurchaseNotifierTests()
    {
        _notifier = new CoursePurchaseNotifier(
            _tarhElahiClientMock.Object,
            _coursePurchaseRepoMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<CoursePurchaseNotifier>.Instance);
    }

    [Fact]
    public async Task NotifyAsync_WhenClientReturnsTrue_ShouldMarkNotificationSent()
    {
        var userId = UserId.New();
        var snapshot = ProductSnapshot.Create("course-1", "Title", 1000m, 10m, 100m, ProductCatalogSources.TarhElahi);
        var purchase = CoursePurchase.Create(userId, snapshot);
        purchase.MarkDebited(Guid.NewGuid());
        var courseDto = new TarhElahiCourseDto
        {
            ExternalId = "course-1",
            Title = "Title",
            PriceRial = 1000m
        };

        _tarhElahiClientMock
            .Setup(c => c.NotifyCoursePurchaseAsync(It.IsAny<CoursePurchaseNotificationDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _notifier.NotifyAsync(purchase, "buyer-ext-1", courseDto, snapshot, CancellationToken.None);

        purchase.Status.Should().Be(CoursePurchaseStatus.NotificationSent);
        _coursePurchaseRepoMock.Verify(r => r.Update(purchase), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyAsync_WhenClientReturnsFalse_ShouldMarkNotificationFailed()
    {
        var userId = UserId.New();
        var snapshot = ProductSnapshot.Create("course-1", "Title", 1000m, 10m, 100m, ProductCatalogSources.TarhElahi);
        var purchase = CoursePurchase.Create(userId, snapshot);
        purchase.MarkDebited(Guid.NewGuid());
        var courseDto = new TarhElahiCourseDto
        {
            ExternalId = "course-1",
            Title = "Title",
            PriceRial = 1000m
        };

        _tarhElahiClientMock
            .Setup(c => c.NotifyCoursePurchaseAsync(It.IsAny<CoursePurchaseNotificationDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _notifier.NotifyAsync(purchase, "buyer-ext-1", courseDto, snapshot, CancellationToken.None);

        purchase.Status.Should().Be(CoursePurchaseStatus.NotificationFailed);
        purchase.NotificationAttempts.Should().Be(1);
        purchase.NotificationFailureReason.Should().NotBeNullOrWhiteSpace();
        _coursePurchaseRepoMock.Verify(r => r.Update(purchase), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyAsync_WhenClientThrowsException_ShouldMarkNotificationFailedAndNotThrow()
    {
        var userId = UserId.New();
        var snapshot = ProductSnapshot.Create("course-1", "Title", 1000m, 10m, 100m, ProductCatalogSources.TarhElahi);
        var purchase = CoursePurchase.Create(userId, snapshot);
        purchase.MarkDebited(Guid.NewGuid());
        var courseDto = new TarhElahiCourseDto
        {
            ExternalId = "course-1",
            Title = "Title",
            PriceRial = 1000m
        };

        _tarhElahiClientMock
            .Setup(c => c.NotifyCoursePurchaseAsync(It.IsAny<CoursePurchaseNotificationDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        await _notifier.NotifyAsync(purchase, "buyer-ext-1", courseDto, snapshot, CancellationToken.None);

        purchase.Status.Should().Be(CoursePurchaseStatus.NotificationFailed);
        purchase.NotificationFailureReason.Should().Contain("Network error");
        _coursePurchaseRepoMock.Verify(r => r.Update(purchase), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void NotificationDto_Serialization_ShouldProduceExpectedContractFormat()
    {
        var dto = new CoursePurchaseNotificationDto
        {
            EventId = Guid.NewGuid(),
            PurchaseId = Guid.NewGuid(),
            BuyerExternalUserId = "user-123",
            Item = new TarhElahiNotificationItemDto
            {
                Type = TarhElahiCatalogItemType.Course,
                ExternalId = "course-abc",
                ParentExternalId = null
            },
            Snapshot = new TarhElahiNotificationSnapshotDto
            {
                ProductSource = ProductCatalogSources.TarhElahi,
                ProductTitle = "آموزش",
                PriceRial = 1000m,
                PriceNoor = 10m,
                ConversionRate = 100m
            },
            PurchasedAtUtc = DateTime.UtcNow
        };

        var json = System.Text.Json.JsonSerializer.Serialize(dto);

        json.Should().Contain("\"course\"");
        json.Should().Contain("\"tarh_elahi\"");
        json.Should().NotContain("\"Course\"");
        json.Should().Contain("\"strapiUserId\":\"user-123\"");
        json.Should().Contain("\"courseId\":\"course-abc\"");
    }
}
