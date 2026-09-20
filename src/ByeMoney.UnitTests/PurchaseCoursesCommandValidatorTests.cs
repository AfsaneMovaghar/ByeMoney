using ByeMoney.Application.Modules.Identity.Users.Interface;
using ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourses;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.Settings;
using ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using FluentAssertions;
using Moq;
using Xunit;
using WalletEntity = ByeMoney.Domain.Modules.Wallet.Wallets.Wallet;

namespace ByeMoney.UnitTests;

public class PurchaseCoursesCommandValidatorTests
{
    private readonly Mock<ICoursePurchaseRepository> _coursePurchaseRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<ITarhElahiIntegrationClient> _tarhElahiClientMock = new();
    private readonly Mock<ISystemSettingRepository> _settingRepoMock = new();
    private readonly Mock<IUserWalletProvisioningService> _walletProvisioningMock = new();

    private readonly PurchaseCoursesCommandValidator _validator;

    public PurchaseCoursesCommandValidatorTests()
    {
        _validator = new PurchaseCoursesCommandValidator(
            _coursePurchaseRepoMock.Object,
            _userRepoMock.Object,
            _tarhElahiClientMock.Object,
            _settingRepoMock.Object,
            _walletProvisioningMock.Object);
    }

    [Fact]
    public async Task ValidateAsync_WhenAllCoursesValidAndBalanceSufficient_ShouldPass()
    {
        // Arrange
        var userId = UserId.New();
        var user = User.CreateFromStrapi("strapi-user-1", "09123456789", confirmed: true, blocked: false);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _settingRepoMock.Setup(r => r.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1000m);

        var c1 = new TarhElahiCourseDto { ExternalId = "c1", Title = "C1", PriceRial = 1_000_000m, Published = true, Available = true, Source = "tarh_elahi" };
        var c2 = new TarhElahiCourseDto { ExternalId = "c2", Title = "C2", PriceRial = 2_000_000m, Published = true, Available = true, Source = "tarh_elahi" };
        _tarhElahiClientMock.Setup(c => c.GetCourseAsync("c1", It.IsAny<CancellationToken>())).ReturnsAsync(c1);
        _tarhElahiClientMock.Setup(c => c.GetCourseAsync("c2", It.IsAny<CancellationToken>())).ReturnsAsync(c2);

        _coursePurchaseRepoMock.Setup(r => r.ExistsByBuyerAndCourseAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var userAccount = Account.CreateUserAccount(userId);
        var wallet = WalletEntity.Create(userAccount.Id, userId);
        wallet.ApplyCredit(5000m); // Needs 3,000 Noor
        _walletProvisioningMock.Setup(s => s.GetOrCreateUserWalletAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(userAccount, wallet));

        var command = new PurchaseCoursesCommand(userId.Value, new[] { "c1", "c2" });

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WhenOneCourseAlreadyPurchased_ShouldFailWithAlreadyPurchasedForThatCourse_AllOrNothing()
    {
        // Arrange
        var userId = UserId.New();
        var user = User.CreateFromStrapi("strapi-user-1", "09123456789", confirmed: true, blocked: false);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _settingRepoMock.Setup(r => r.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1000m);

        // c1 is already purchased by this user!
        _coursePurchaseRepoMock.Setup(r => r.ExistsByBuyerAndCourseAsync(userId, "c1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _coursePurchaseRepoMock.Setup(r => r.ExistsByBuyerAndCourseAsync(userId, "c2", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var c2 = new TarhElahiCourseDto { ExternalId = "c2", Title = "C2", PriceRial = 2_000_000m, Published = true, Available = true, Source = "tarh_elahi" };
        _tarhElahiClientMock.Setup(c => c.GetCourseAsync("c2", It.IsAny<CancellationToken>())).ReturnsAsync(c2);

        var command = new PurchaseCoursesCommand(userId.Value, new[] { "c1", "c2" });

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ExternalCourseIds[c1]" && e.ErrorMessage == ApplicationErrors.CoursePurchase_AlreadyPurchased);
        // Balance check must NOT have run (no Wallet error)
        result.Errors.Should().NotContain(e => e.PropertyName == "Wallet");
    }

    [Fact]
    public async Task ValidateAsync_WhenOneCourseNotFound_ShouldFailWithCourseNotFoundForThatCourse()
    {
        // Arrange
        var userId = UserId.New();
        var user = User.CreateFromStrapi("strapi-user-1", "09123456789", confirmed: true, blocked: false);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _settingRepoMock.Setup(r => r.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1000m);

        _coursePurchaseRepoMock.Setup(r => r.ExistsByBuyerAndCourseAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var c1 = new TarhElahiCourseDto { ExternalId = "c1", Title = "C1", PriceRial = 1_000_000m, Published = true, Available = true, Source = "tarh_elahi" };
        _tarhElahiClientMock.Setup(c => c.GetCourseAsync("c1", It.IsAny<CancellationToken>())).ReturnsAsync(c1);
        _tarhElahiClientMock.Setup(c => c.GetCourseAsync("non-existent", It.IsAny<CancellationToken>())).ReturnsAsync((TarhElahiCourseDto?)null);

        var command = new PurchaseCoursesCommand(userId.Value, new[] { "c1", "non-existent" });

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ExternalCourseIds[non-existent]" && e.ErrorMessage == ApplicationErrors.CoursePurchase_CourseNotFound);
    }

    [Fact]
    public async Task ValidateAsync_WhenOneCourseUnavailable_ShouldFailWithCourseNotAvailable()
    {
        // Arrange
        var userId = UserId.New();
        var user = User.CreateFromStrapi("strapi-user-1", "09123456789", confirmed: true, blocked: false);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _settingRepoMock.Setup(r => r.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1000m);
        _coursePurchaseRepoMock.Setup(r => r.ExistsByBuyerAndCourseAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var c1 = new TarhElahiCourseDto { ExternalId = "c1", Title = "C1", PriceRial = 1_000_000m, Published = false, Available = false, Source = "tarh_elahi" };
        _tarhElahiClientMock.Setup(c => c.GetCourseAsync("c1", It.IsAny<CancellationToken>())).ReturnsAsync(c1);

        var command = new PurchaseCoursesCommand(userId.Value, new[] { "c1" });

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ExternalCourseIds[c1]" && e.ErrorMessage == ApplicationErrors.CoursePurchase_CourseNotAvailable);
    }

    [Fact]
    public async Task ValidateAsync_WhenOneCourseIsFree_ShouldFailWithFreeCourseNotPurchasable()
    {
        // Arrange
        var userId = UserId.New();
        var user = User.CreateFromStrapi("strapi-user-1", "09123456789", confirmed: true, blocked: false);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _settingRepoMock.Setup(r => r.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1000m);
        _coursePurchaseRepoMock.Setup(r => r.ExistsByBuyerAndCourseAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var c1 = new TarhElahiCourseDto { ExternalId = "c1", Title = "Free Course", PriceRial = 0m, Published = true, Available = true, Source = "tarh_elahi" };
        _tarhElahiClientMock.Setup(c => c.GetCourseAsync("c1", It.IsAny<CancellationToken>())).ReturnsAsync(c1);

        var command = new PurchaseCoursesCommand(userId.Value, new[] { "c1" });

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ExternalCourseIds[c1]" && e.ErrorMessage == ApplicationErrors.CoursePurchase_FreeCourseNotPurchasable);
    }

    [Fact]
    public async Task ValidateAsync_WhenWalletBalanceInsufficientForBasket_ShouldFailWithCustomStateContainingAllItemsSnapshots()
    {
        // Arrange
        var userId = UserId.New();
        var user = User.CreateFromStrapi("strapi-user-1", "09123456789", confirmed: true, blocked: false);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _settingRepoMock.Setup(r => r.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1000m);
        _coursePurchaseRepoMock.Setup(r => r.ExistsByBuyerAndCourseAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var c1 = new TarhElahiCourseDto { ExternalId = "c1", Title = "C1", PriceRial = 2_000_000m, Published = true, Available = true, Source = "tarh_elahi" };
        var c2 = new TarhElahiCourseDto { ExternalId = "c2", Title = "C2", PriceRial = 3_000_000m, Published = true, Available = true, Source = "tarh_elahi" };
        _tarhElahiClientMock.Setup(c => c.GetCourseAsync("c1", It.IsAny<CancellationToken>())).ReturnsAsync(c1);
        _tarhElahiClientMock.Setup(c => c.GetCourseAsync("c2", It.IsAny<CancellationToken>())).ReturnsAsync(c2);

        // Total price = 2,000 + 3,000 = 5,000 Noor. User has only 1,000 Noor. Shortfall = 4,000 Noor.
        var userAccount = Account.CreateUserAccount(userId);
        var wallet = WalletEntity.Create(userAccount.Id, userId);
        wallet.ApplyCredit(1_000m);
        _walletProvisioningMock.Setup(s => s.GetOrCreateUserWalletAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(userAccount, wallet));

        var command = new PurchaseCoursesCommand(userId.Value, new[] { "c1", "c2" });

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        var walletError = result.Errors.Single(e => e.PropertyName == "Wallet");
        walletError.ErrorCode.Should().Be("INSUFFICIENT_NOOR_BALANCE");
        walletError.CustomState.Should().BeOfType<InsufficientBalanceFailureState>();

        var state = (InsufficientBalanceFailureState)walletError.CustomState!;
        state.CurrentBalanceInNoor.Should().Be(1_000m);
        state.PriceInNoor.Should().Be(5_000m);
        state.ShortfallInNoor.Should().Be(4_000m);
        state.ShortfallInRial.Should().Be(4_000_000m);
        state.PendingItems.Should().HaveCount(2);
        state.PendingItems![0].ExternalId.Should().Be("c1");
        state.PendingItems[1].ExternalId.Should().Be("c2");
    }

    [Fact]
    public async Task ValidateAsync_WhenDuplicateCourseIdsInBasket_ShouldFailValidation()
    {
        // Arrange
        var command = new PurchaseCoursesCommand(Guid.NewGuid(), new[] { "c1", "c1" });

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(PurchaseCoursesCommand.ExternalCourseIds) && e.ErrorMessage == ApplicationErrors.CoursePurchase_DuplicateCoursesInBasket);
    }

    [Fact]
    public async Task ValidateAsync_WhenRetriedBasketRequestWhereAllCoursesAlreadyPurchased_ShouldRejectBothCoursesWithoutDoubleCharging()
    {
        // Arrange (User already owns both c1 and c2 from a previously successful checkout)
        var userId = UserId.New();
        var user = User.CreateFromStrapi("strapi-user-1", "09123456789", confirmed: true, blocked: false);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _settingRepoMock.Setup(r => r.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1000m);

        _coursePurchaseRepoMock.Setup(r => r.ExistsByBuyerAndCourseAsync(userId, "c1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _coursePurchaseRepoMock.Setup(r => r.ExistsByBuyerAndCourseAsync(userId, "c2", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var command = new PurchaseCoursesCommand(userId.Value, new[] { "c1", "c2" });

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ExternalCourseIds[c1]" && e.ErrorMessage == ApplicationErrors.CoursePurchase_AlreadyPurchased);
        result.Errors.Should().Contain(e => e.PropertyName == "ExternalCourseIds[c2]" && e.ErrorMessage == ApplicationErrors.CoursePurchase_AlreadyPurchased);
        // Balance check must NOT be invoked
        _walletProvisioningMock.Verify(s => s.GetOrCreateUserWalletAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ValidateAsync_WhenPartiallyOverlappingBasketRequest_ShouldRejectWithAlreadyPurchasedForExistingCourse_AllOrNothing()
    {
        // Arrange (User already owns c1, but c2 is new)
        var userId = UserId.New();
        var user = User.CreateFromStrapi("strapi-user-1", "09123456789", confirmed: true, blocked: false);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _settingRepoMock.Setup(r => r.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1000m);

        _coursePurchaseRepoMock.Setup(r => r.ExistsByBuyerAndCourseAsync(userId, "c1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _coursePurchaseRepoMock.Setup(r => r.ExistsByBuyerAndCourseAsync(userId, "c2", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var c2 = new TarhElahiCourseDto { ExternalId = "c2", Title = "C2", PriceRial = 2_000_000m, Published = true, Available = true, Source = "tarh_elahi" };
        _tarhElahiClientMock.Setup(c => c.GetCourseAsync("c2", It.IsAny<CancellationToken>())).ReturnsAsync(c2);

        var command = new PurchaseCoursesCommand(userId.Value, new[] { "c1", "c2" });

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ExternalCourseIds[c1]" && e.ErrorMessage == ApplicationErrors.CoursePurchase_AlreadyPurchased);
        result.Errors.Should().NotContain(e => e.PropertyName == "ExternalCourseIds[c2]");
    }
}
