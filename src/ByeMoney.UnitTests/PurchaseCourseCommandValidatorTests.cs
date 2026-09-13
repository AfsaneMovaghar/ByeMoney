using ByeMoney.Application.Modules.Identity.Users.Interface;
using ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourse;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.Settings;
using ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using FluentAssertions;
using Moq;
using Xunit;
using WalletEntity = ByeMoney.Domain.Modules.Wallet.Wallets.Wallet;

namespace ByeMoney.UnitTests;

public class PurchaseCourseCommandValidatorTests
{
    private readonly Mock<ICoursePurchaseRepository> _coursePurchaseRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<ITarhElahiIntegrationClient> _tarhElahiClientMock = new();
    private readonly Mock<ISystemSettingRepository> _settingRepoMock = new();
    private readonly Mock<IUserWalletProvisioningService> _walletProvisioningMock = new();

    private readonly PurchaseCourseCommandValidator _validator;

    public PurchaseCourseCommandValidatorTests()
    {
        _coursePurchaseRepoMock
            .Setup(r => r.ExistsByBuyerAndCourseAsync(It.IsAny<UserId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var activeUser = User.CreateFromStrapi("strapi-1", "09121111111", confirmed: true, blocked: false);
        _userRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeUser);

        _tarhElahiClientMock
            .Setup(c => c.GetCourseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TarhElahiCourseDto
            {
                ExternalId = "course-123",
                Title = "Course Title",
                PriceRial = 1_000_000m,
                Published = true,
                Available = true
            });

        _settingRepoMock
            .Setup(s => s.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1000m);

        var userId = UserId.New();
        var userAccount = Account.CreateUserAccount(userId);
        var wallet = WalletEntity.Create(userAccount.Id, userId);
        wallet.ApplyCredit(5000m);

        _walletProvisioningMock
            .Setup(s => s.GetOrCreateUserWalletAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(userAccount, wallet));

        _validator = new PurchaseCourseCommandValidator(
            _coursePurchaseRepoMock.Object,
            _userRepoMock.Object,
            _tarhElahiClientMock.Object,
            _settingRepoMock.Object,
            _walletProvisioningMock.Object);
    }

    [Fact]
    public async Task ValidateAsync_WhenBuyerUserIdIsEmpty_ShouldHaveValidationError()
    {
        var command = new PurchaseCourseCommand(Guid.Empty, "course-123");
        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(PurchaseCourseCommand.BuyerUserId) &&
            e.ErrorMessage == ApplicationErrors.CoursePurchase_BuyerUserIdRequired);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateAsync_WhenExternalCourseIdIsEmpty_ShouldHaveValidationError(string invalidCourseId)
    {
        var command = new PurchaseCourseCommand(Guid.NewGuid(), invalidCourseId);
        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(PurchaseCourseCommand.ExternalCourseId) &&
            e.ErrorMessage == ApplicationErrors.CoursePurchase_ExternalCourseIdRequired);
    }

    [Fact]
    public async Task ValidateAsync_WhenExternalCourseIdExceedsMaxLength_ShouldHaveValidationError()
    {
        var longCourseId = new string('c', 101);
        var command = new PurchaseCourseCommand(Guid.NewGuid(), longCourseId);
        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(PurchaseCourseCommand.ExternalCourseId) &&
            e.ErrorMessage == ApplicationErrors.CoursePurchase_ExternalCourseIdMaxLength);
    }

    [Fact]
    public async Task ValidateAsync_WhenCourseAlreadyPurchased_ShouldHaveAlreadyPurchasedError()
    {
        var buyerUserId = Guid.NewGuid();
        var courseId = "course-already-owned";

        _coursePurchaseRepoMock
            .Setup(r => r.ExistsByBuyerAndCourseAsync(new UserId(buyerUserId), courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new PurchaseCourseCommand(buyerUserId, courseId);
        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == ApplicationErrors.CoursePurchase_AlreadyPurchased);
    }

    [Fact]
    public async Task ValidateAsync_WhenUserNotFound_ShouldHaveUserNotFoundError()
    {
        _userRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new PurchaseCourseCommand(Guid.NewGuid(), "course-123");
        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == ApplicationErrors.CoursePurchase_UserNotFound);
    }

    [Fact]
    public async Task ValidateAsync_WhenUserNotActive_ShouldHaveUserNotActiveError()
    {
        var blockedUser = User.CreateFromStrapi("strapi-1", "09121111111", confirmed: true, blocked: true);
        _userRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(blockedUser);

        var command = new PurchaseCourseCommand(Guid.NewGuid(), "course-123");
        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == ApplicationErrors.CoursePurchase_UserNotActive);
    }

    [Fact]
    public async Task ValidateAsync_WhenCourseNotFoundInTarhElahi_ShouldHaveCourseNotFoundError()
    {
        _tarhElahiClientMock
            .Setup(c => c.GetCourseAsync("non-existent-course", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TarhElahiCourseDto?)null);

        var command = new PurchaseCourseCommand(Guid.NewGuid(), "non-existent-course");
        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == ApplicationErrors.CoursePurchase_CourseNotFound);
    }

    [Fact]
    public async Task ValidateAsync_WhenCourseIsNotPublishedOrAvailable_ShouldHaveCourseNotAvailableError()
    {
        _tarhElahiClientMock
            .Setup(c => c.GetCourseAsync("unpublished-course", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TarhElahiCourseDto
            {
                ExternalId = "unpublished-course",
                Published = false,
                Available = false,
                PriceRial = 1_000_000m
            });

        var command = new PurchaseCourseCommand(Guid.NewGuid(), "unpublished-course");
        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == ApplicationErrors.CoursePurchase_CourseNotAvailable);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    public async Task ValidateAsync_WhenCoursePriceIsZeroOrNegative_ShouldHaveFreeCourseNotPurchasableError(decimal priceRial)
    {
        _tarhElahiClientMock
            .Setup(c => c.GetCourseAsync("free-course", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TarhElahiCourseDto
            {
                ExternalId = "free-course",
                Published = true,
                Available = true,
                PriceRial = priceRial
            });

        var command = new PurchaseCourseCommand(Guid.NewGuid(), "free-course");
        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == ApplicationErrors.CoursePurchase_FreeCourseNotPurchasable);
    }

    [Fact]
    public async Task ValidateAsync_WhenConversionRateIsZeroOrNegative_ShouldHaveInvalidConversionRateError()
    {
        _settingRepoMock
            .Setup(s => s.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var command = new PurchaseCourseCommand(Guid.NewGuid(), "course-123");
        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == ApplicationErrors.CoursePurchase_InvalidConversionRate);
    }

    [Fact]
    public async Task ValidateAsync_WhenWalletBalanceIsInsufficient_ShouldHaveInsufficientBalanceError()
    {
        var userId = UserId.New();
        var userAccount = Account.CreateUserAccount(userId);
        var wallet = WalletEntity.Create(userAccount.Id, userId);
        wallet.ApplyCredit(500m); // only 500, but course price in Noor is 1000

        _walletProvisioningMock
            .Setup(s => s.GetOrCreateUserWalletAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(userAccount, wallet));

        var command = new PurchaseCourseCommand(Guid.NewGuid(), "course-123");
        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage.Contains("500") && e.ErrorMessage.Contains("1000"));
    }

    [Fact]
    public async Task ValidateAsync_WhenValidAndSufficientBalance_ShouldBeValid()
    {
        var command = new PurchaseCourseCommand(Guid.NewGuid(), "course-123");
        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }
}
