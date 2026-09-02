using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Commands.SyncUserFromStrapi;
using ByeMoney.Application.Modules.Identity.Users.Interface;
using ByeMoney.Domain.Modules.Identity.Users;
using FluentAssertions;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace ByeMoney.UnitTests;

public class SyncUserFromStrapiTests
{
    [Fact]
    public void CreateFromStrapi_ShouldInitializeUserWithDefaultValuesAndNullPhoneAndDisplayName()
    {
        // Act
        var user = User.CreateFromStrapi(12345);

        // Assert
        user.StrapiUserId.Should().Be(12345);
        user.DisplayName.Should().BeNull();
        user.Phone.Should().BeNull();
        user.Role.Should().Be(string.Empty);
        user.Status.Should().Be(UserStatus.Active);
        user.UserType.Should().Be(UserType.Normal);
        user.ProfileSyncedAt.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void MarkProfileSynced_ShouldUpdateProfileSyncedAtAndUpdatedAt()
    {
        // Arrange
        var user = User.CreateFromStrapi(12345);

        // Act
        user.MarkProfileSynced();

        // Assert
        user.ProfileSyncedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        user.UpdatedAt.Should().NotBeNull();
        user.UpdatedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Validator_ShouldHaveError_WhenStrapiUserIdIsZeroOrNegative()
    {
        // Arrange
        var validator = new SyncUserFromStrapiCommandValidator();

        // Act & Assert
        var resultZero = await validator.TestValidateAsync(new SyncUserFromStrapiCommand(0));
        resultZero.ShouldHaveValidationErrorFor(x => x.StrapiUserId);

        var resultNegative = await validator.TestValidateAsync(new SyncUserFromStrapiCommand(-1));
        resultNegative.ShouldHaveValidationErrorFor(x => x.StrapiUserId);

        var resultValid = await validator.TestValidateAsync(new SyncUserFromStrapiCommand(10));
        resultValid.ShouldNotHaveValidationErrorFor(x => x.StrapiUserId);
    }

    [Fact]
    public async Task Handler_ShouldUpdateAndSync_WhenUserExists()
    {
        // Arrange
        var userRepositoryMock = new Mock<IUserRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();

        var existingUser = User.CreateFromStrapi(42);
        userRepositoryMock
            .Setup(r => r.GetByStrapiUserIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        var handler = new SyncUserFromStrapiCommandHandler(userRepositoryMock.Object, unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(new SyncUserFromStrapiCommand(42), CancellationToken.None);

        // Assert
        result.Should().Be(existingUser.Id.Value);
        userRepositoryMock.Verify(r => r.Update(existingUser), Times.Once);
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handler_ShouldCreateNewUser_WhenUserDoesNotExist()
    {
        // Arrange
        var userRepositoryMock = new Mock<IUserRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();

        userRepositoryMock
            .Setup(r => r.GetByStrapiUserIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new SyncUserFromStrapiCommandHandler(userRepositoryMock.Object, unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(new SyncUserFromStrapiCommand(99), CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        userRepositoryMock.Verify(r => r.AddAsync(It.Is<User>(u => u.StrapiUserId == 99 && u.DisplayName == null && u.Phone == null), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
