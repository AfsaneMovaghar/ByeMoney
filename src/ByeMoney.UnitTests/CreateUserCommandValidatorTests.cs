using ByeMoney.Application.Modules.Identity.Users.Commands.CreateUser;
using ByeMoney.Application.Modules.Identity.Users.Interface;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Identity.Users;
using FluentAssertions;
using Moq;
using Xunit;

namespace ByeMoney.UnitTests;

public class CreateUserCommandValidatorTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly CreateUserCommandValidator _validator;

    public CreateUserCommandValidatorTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _userRepositoryMock
            .Setup(r => r.ExistsByExternalUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.ExistsByPhoneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _validator = new CreateUserCommandValidator(_userRepositoryMock.Object);
    }

    [Fact]
    public async Task ValidateAsync_WhenPhoneAlreadyExists_ShouldHaveValidationError()
    {
        // Arrange
        const string existingPhone = "09123456789";
        _userRepositoryMock
            .Setup(r => r.ExistsByPhoneAsync(existingPhone, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new CreateUserCommand(
            ExternalUserId: "ext-1",
            Phone: existingPhone);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateUserCommand.Phone) &&
            e.ErrorMessage == ApplicationErrors.User_PhoneAlreadyExists);

        _userRepositoryMock.Verify(r => r.ExistsByPhoneAsync(existingPhone, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("08123456789")]
    [InlineData("0912345678")]
    [InlineData("091234567890")]
    [InlineData("abcdefghijk")]
    public async Task ValidateAsync_WhenPhoneFormatIsInvalid_ShouldHaveFormatErrorAndNotCallRepository(string invalidPhone)
    {
        // Arrange
        var command = new CreateUserCommand(
            ExternalUserId: "ext-1",
            Phone: invalidPhone);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateUserCommand.Phone) &&
            e.ErrorMessage == ApplicationErrors.User_PhoneInvalidFormat);

        _userRepositoryMock.Verify(r => r.ExistsByPhoneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ValidateAsync_WhenPhoneIsUnique_ShouldBeValid()
    {
        // Arrange
        const string uniquePhone = "09123456789";
        var command = new CreateUserCommand(
            ExternalUserId: "ext-1",
            Phone: uniquePhone);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
        _userRepositoryMock.Verify(r => r.ExistsByPhoneAsync(uniquePhone, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateAsync_WhenPhoneIsNull_ShouldNotValidatePhone()
    {
        // Arrange
        var command = new CreateUserCommand(
            ExternalUserId: "ext-1",
            Phone: null);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
        _userRepositoryMock.Verify(r => r.ExistsByPhoneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

