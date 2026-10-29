using System.Net;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interfaces;
using ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;
using ByeMoney.Application.Modules.TarhElahiIntegration.Exceptions;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Infrastructure.Modules.Identity.Services;
using ByeMoney.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ByeMoney.UnitTests;

public sealed class UserProvisioningServiceTests
{
    private const string ExternalUserId = "strapi-doc-provisioning-1";

    [Fact]
    public async Task EnsureUser_WhenUserExists_ReturnsExistingUserWithoutStrapiCallOrGift()
    {
        var existingUser = User.CreateFromStrapi(ExternalUserId, confirmed: true);
        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(x => x.GetByExternalUserIdAsync(ExternalUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        var tarhElahiClient = new Mock<ITarhElahiIntegrationClient>();
        var welcomeGift = new Mock<IWelcomeGiftService>();
        var unitOfWork = new Mock<IUnitOfWork>();

        using var context = CreateUnconnectedContext();
        var service = CreateService(userRepository, unitOfWork, tarhElahiClient, welcomeGift, context);

        var result = await service.EnsureUserFromExternalAsync(ExternalUserId);

        result.Created.Should().BeFalse();
        result.User.Should().BeSameAs(existingUser);
        tarhElahiClient.Verify(x => x.GetUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        welcomeGift.Verify(x => x.GrantWelcomeGiftIfEligibleAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureUser_WhenUserIsNew_CreatesUserAndGrantsWelcomeGiftOnce()
    {
        var dependencies = CreateDependencies();
        var service = CreateService(
            dependencies.UserRepository,
            dependencies.UnitOfWork,
            dependencies.TarhElahiClient,
            dependencies.WelcomeGift,
            dependencies.Context);

        var result = await service.EnsureUserFromExternalAsync(ExternalUserId);

        result.Created.Should().BeTrue();
        result.User.ExternalUserId.Should().Be(ExternalUserId);
        dependencies.UserRepository.Verify(
            x => x.AddAsync(It.Is<User>(u => u.ExternalUserId == ExternalUserId), It.IsAny<CancellationToken>()),
            Times.Once);
        dependencies.WelcomeGift.Verify(
            x => x.GrantWelcomeGiftIfEligibleAsync(It.Is<User>(u => u.ExternalUserId == ExternalUserId), It.IsAny<CancellationToken>()),
            Times.Once);
        dependencies.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureUser_WhenCalledAgainAfterCreation_DoesNotGrantSecondGift()
    {
        var dependencies = CreateDependencies();
        var service = CreateService(
            dependencies.UserRepository,
            dependencies.UnitOfWork,
            dependencies.TarhElahiClient,
            dependencies.WelcomeGift,
            dependencies.Context);

        var first = await service.EnsureUserFromExternalAsync(ExternalUserId);
        var second = await service.EnsureUserFromExternalAsync(ExternalUserId);

        first.Created.Should().BeTrue();
        second.Created.Should().BeFalse();
        second.User.Id.Should().Be(first.User.Id);
        dependencies.WelcomeGift.Verify(
            x => x.GrantWelcomeGiftIfEligibleAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);
        dependencies.UserRepository.Verify(
            x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnsureUser_WhenTransientFailureHappensOnce_RetriesAndCreatesUser()
    {
        var dependencies = CreateDependencies();
        dependencies.TarhElahiClient
            .SetupSequence(x => x.GetUserAsync(ExternalUserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TarhElahiUnavailableException("Tarh-e-Elahi timed out."))
            .ReturnsAsync(CreateStrapiUser());

        var service = CreateService(
            dependencies.UserRepository,
            dependencies.UnitOfWork,
            dependencies.TarhElahiClient,
            dependencies.WelcomeGift,
            dependencies.Context);

        var result = await service.EnsureUserFromExternalAsync(ExternalUserId);

        result.Created.Should().BeTrue();
        dependencies.TarhElahiClient.Verify(
            x => x.GetUserAsync(ExternalUserId, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        dependencies.WelcomeGift.Verify(
            x => x.GrantWelcomeGiftIfEligibleAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);
        dependencies.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureUser_WhenTransientFailurePersists_ThrowsAndCreatesNothing()
    {
        var dependencies = CreateDependencies();
        dependencies.TarhElahiClient
            .Setup(x => x.GetUserAsync(ExternalUserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TarhElahiUnavailableException("Tarh-e-Elahi is unavailable."));

        var service = CreateService(
            dependencies.UserRepository,
            dependencies.UnitOfWork,
            dependencies.TarhElahiClient,
            dependencies.WelcomeGift,
            dependencies.Context);

        var act = async () => await service.EnsureUserFromExternalAsync(ExternalUserId);

        await act.Should().ThrowAsync<TarhElahiUnavailableException>();
        dependencies.TarhElahiClient.Verify(
            x => x.GetUserAsync(ExternalUserId, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        dependencies.UserRepository.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        dependencies.WelcomeGift.Verify(
            x => x.GrantWelcomeGiftIfEligibleAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
        dependencies.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureUser_WhenStrapiRejectsAuthentication_DoesNotRetry()
    {
        var dependencies = CreateDependencies();
        dependencies.TarhElahiClient
            .Setup(x => x.GetUserAsync(ExternalUserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TarhElahiUnavailableException(
                "Tarh-e-Elahi rejected the integration token.",
                statusCode: HttpStatusCode.Unauthorized));

        var service = CreateService(
            dependencies.UserRepository,
            dependencies.UnitOfWork,
            dependencies.TarhElahiClient,
            dependencies.WelcomeGift,
            dependencies.Context);

        var act = async () => await service.EnsureUserFromExternalAsync(ExternalUserId);

        await act.Should().ThrowAsync<TarhElahiUnavailableException>();
        dependencies.TarhElahiClient.Verify(
            x => x.GetUserAsync(ExternalUserId, It.IsAny<CancellationToken>()),
            Times.Once);
        dependencies.UserRepository.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        dependencies.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureUser_WhenStrapiReturnsNotFound_ThrowsNotFoundWithoutRetry()
    {
        var dependencies = CreateDependencies();
        dependencies.TarhElahiClient
            .Setup(x => x.GetUserAsync(ExternalUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TarhElahiUserDto?)null);

        var service = CreateService(
            dependencies.UserRepository,
            dependencies.UnitOfWork,
            dependencies.TarhElahiClient,
            dependencies.WelcomeGift,
            dependencies.Context);

        var act = async () => await service.EnsureUserFromExternalAsync(ExternalUserId);

        await act.Should().ThrowAsync<NotFoundException>();
        dependencies.TarhElahiClient.Verify(
            x => x.GetUserAsync(ExternalUserId, It.IsAny<CancellationToken>()),
            Times.Once);
        dependencies.UserRepository.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        dependencies.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static TestDependencies CreateDependencies()
    {
        var storedUser = (User?)null;
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(x => x.GetByExternalUserIdAsync(ExternalUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => storedUser);
        userRepository
            .Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => storedUser = user)
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var tarhElahiClient = new Mock<ITarhElahiIntegrationClient>();
        tarhElahiClient
            .Setup(x => x.GetUserAsync(ExternalUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateStrapiUser());

        var welcomeGift = new Mock<IWelcomeGiftService>();
        welcomeGift
            .Setup(x => x.GrantWelcomeGiftIfEligibleAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new TestDependencies(userRepository, unitOfWork, tarhElahiClient, welcomeGift, CreateUnconnectedContext());
    }

    private static UserProvisioningService CreateService(
        Mock<IUserRepository> userRepository,
        Mock<IUnitOfWork> unitOfWork,
        Mock<ITarhElahiIntegrationClient> tarhElahiClient,
        Mock<IWelcomeGiftService> welcomeGift,
        ApplicationDbContext context)
        => new(
            userRepository.Object,
            unitOfWork.Object,
            tarhElahiClient.Object,
            welcomeGift.Object,
            context,
            Mock.Of<ILogger<UserProvisioningService>>());

    private static ApplicationDbContext CreateUnconnectedContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options);

    private static TarhElahiUserDto CreateStrapiUser()
        => new()
        {
            ExternalUserId = ExternalUserId,
            PhoneNumber = "09120000000",
            Email = "provisioning@test.com",
            FirstName = "Ali",
            LastName = "Rezaei",
            Confirmed = true,
            Blocked = false
        };

    private sealed record TestDependencies(
        Mock<IUserRepository> UserRepository,
        Mock<IUnitOfWork> UnitOfWork,
        Mock<ITarhElahiIntegrationClient> TarhElahiClient,
        Mock<IWelcomeGiftService> WelcomeGift,
        ApplicationDbContext Context);
}
