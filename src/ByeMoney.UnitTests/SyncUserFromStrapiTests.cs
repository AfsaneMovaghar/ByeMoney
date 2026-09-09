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
    public void CreateInitial_ShouldInitializeUserWithDefaultValuesAndIsActiveTrue()
    {
        // Act
        var user = User.CreateInitial("strapi-doc-123");

        // Assert
        user.ExternalUserId.Should().Be("strapi-doc-123");
        user.DisplayName.Should().BeNull();
        user.FirstName.Should().BeNull();
        user.LastName.Should().BeNull();
        user.Phone.Should().BeNull();
        user.Email.Should().BeNull();
        user.IsActive.Should().BeTrue();
        user.UserType.Should().Be(UserType.Normal);
        user.ProfileSyncedAt.Should().Be(DateTime.MinValue);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public void CreateFromStrapi_ShouldCalculateIsActive_BasedOnConfirmedAndBlocked(
        bool confirmed, bool blocked, bool expectedIsActive)
    {
        // Act
        var user = User.CreateFromStrapi(
            "strapi-doc-123",
            phone: "09123456789",
            email: "test@example.com",
            firstName: "Ali",
            lastName: "Rezaei",
            confirmed: confirmed,
            blocked: blocked);

        // Assert
        user.IsActive.Should().Be(expectedIsActive);
    }

    [Fact]
    public void MarkProfileSynced_ShouldUpdateProfileSyncedAtAndUpdatedAt()
    {
        // Arrange
        var user = User.CreateFromStrapi("strapi-doc-123");

        // Act
        user.MarkProfileSynced();

        // Assert
        user.ProfileSyncedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        user.UpdatedAt.Should().NotBeNull();
        user.UpdatedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SyncProfile_ShouldUpdateIsActiveBasedOnConfirmedAndBlocked()
    {
        // Arrange
        var user = User.CreateInitial("strapi-doc-123");
        user.IsActive.Should().BeTrue();

        // Act - user blocked
        user.SyncProfile("09123456789", "test@example.com", "Ali", "Rezaei", confirmed: true, blocked: true);

        // Assert
        user.IsActive.Should().BeFalse();
        user.FirstName.Should().Be("Ali");
        user.LastName.Should().Be("Rezaei");
        user.Phone.Should().Be("09123456789");
        user.Email.Should().Be("test@example.com");

        // Act - user unblocked and confirmed
        user.SyncProfile("09123456789", "test@example.com", "Ali", "Rezaei", confirmed: true, blocked: false);

        // Assert
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Validator_ShouldHaveError_WhenExternalUserIdIsEmpty()
    {
        // Arrange
        var validator = new SyncUserFromStrapiCommandValidator();

        // Act & Assert
        var resultEmpty = await validator.TestValidateAsync(new SyncUserFromStrapiCommand(""));
        resultEmpty.ShouldHaveValidationErrorFor(x => x.ExternalUserId);

        var resultWhitespace = await validator.TestValidateAsync(new SyncUserFromStrapiCommand("   "));
        resultWhitespace.ShouldHaveValidationErrorFor(x => x.ExternalUserId);

        var resultValid = await validator.TestValidateAsync(new SyncUserFromStrapiCommand("valid-doc-id"));
        resultValid.ShouldNotHaveValidationErrorFor(x => x.ExternalUserId);
    }

    [Fact]
    public async Task Handler_ShouldUpdateAndSync_WhenUserExists()
    {
        // Arrange
        var userRepositoryMock = new Mock<IUserRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();

        var existingUser = User.CreateFromStrapi("user-42");
        userRepositoryMock
            .Setup(r => r.GetByExternalUserIdAsync("user-42", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        var handler = new SyncUserFromStrapiCommandHandler(userRepositoryMock.Object, unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(new SyncUserFromStrapiCommand("user-42"), CancellationToken.None);

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
            .Setup(r => r.GetByExternalUserIdAsync("user-99", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new SyncUserFromStrapiCommandHandler(userRepositoryMock.Object, unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(new SyncUserFromStrapiCommand("user-99"), CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        userRepositoryMock.Verify(r => r.AddAsync(It.Is<User>(u => u.ExternalUserId == "user-99" && u.DisplayName == null && u.Phone == null), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
