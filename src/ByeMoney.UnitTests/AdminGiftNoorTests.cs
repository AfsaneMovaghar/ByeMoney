using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.AdminGrantGiftNoor;
using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Gifts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using ByeMoney.Domain.Resources;
using FluentAssertions;
using Moq;

namespace ByeMoney.UnitTests;

public sealed class AdminGiftNoorTests
{
    private readonly Mock<IUserProvisioningService> _userProvisioning = new();
    private readonly Mock<IWalletRepository> _walletRepo = new();
    private readonly Mock<IGiftGrantRepository> _giftGrantRepo = new();
    private readonly Mock<IUserWalletProvisioningService> _provisioning = new();
    private readonly Mock<IRepository<LedgerEntry, LedgerEntryId>> _ledgerRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private readonly Account _systemAccount = Account.CreateSystemAccount();
    private readonly List<LedgerEntry> _recordedLedger = [];
    private GiftGrantBatch? _storedBatch;

    private AdminGrantGiftNoorCommandHandler CreateHandler()
    {
        _provisioning.Setup(x => x.GetOrCreateSystemAccountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_systemAccount);

        _giftGrantRepo.Setup(x => x.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => _storedBatch?.IdempotencyKey == key ? _storedBatch : null);

        _giftGrantRepo.Setup(x => x.AddBatchAsync(It.IsAny<GiftGrantBatch>(), It.IsAny<CancellationToken>()))
            .Callback<GiftGrantBatch, CancellationToken>((batch, _) => _storedBatch = batch)
            .Returns(Task.CompletedTask);

        _ledgerRepo.Setup(x => x.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerEntry, CancellationToken>((entry, _) => _recordedLedger.Add(entry))
            .Returns(Task.CompletedTask);

        var postingService = new ByeMoney.Application.Modules.Wallet.Services.WalletPostingService(_ledgerRepo.Object, _walletRepo.Object);
        var noorCreditService = new ByeMoney.Application.Modules.Wallet.Services.NoorCreditService(_provisioning.Object, postingService);
        return new AdminGrantGiftNoorCommandHandler(_userProvisioning.Object, _giftGrantRepo.Object, noorCreditService, _unitOfWork.Object);
    }

    [Fact]
    public async Task Success_GrantsGift_RecordsBalancedLedger_AndIncreasesGiftBalance()
    {
        var handler = CreateHandler();
        var user1 = User.CreateFromStrapi("usr-1", confirmed: true, blocked: false);
        var user2 = User.CreateFromStrapi("usr-2", confirmed: true, blocked: false);

        _userProvisioning.Setup(x => x.EnsureUserFromExternalAsync("usr-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProvisioningResult(user1, Created: false));
        _userProvisioning.Setup(x => x.EnsureUserFromExternalAsync("usr-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProvisioningResult(user2, Created: false));

        var wallet1 = Wallet.Create(Account.CreateUserAccount(user1.Id).Id, user1.Id);
        var wallet2 = Wallet.Create(Account.CreateUserAccount(user2.Id).Id, user2.Id);

        _provisioning.Setup(x => x.GetOrCreateUserWalletAsync(user1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(Account.CreateUserAccount(user1.Id), wallet1));
        _provisioning.Setup(x => x.GetOrCreateUserWalletAsync(user2.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(Account.CreateUserAccount(user2.Id), wallet2));

        var command = new AdminGrantGiftNoorCommand(["usr-1", "usr-2"], 250m, "هدیه مسابقه", "key-1", Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Response.Should().NotBeNull();
        result.Response!.TotalRecipients.Should().Be(2);
        result.Response.TotalAmountNoor.Should().Be(500m);
        result.Response.AmountNoorPerUser.Should().Be(250m);

        wallet1.GiftBalance.Should().Be(250m);
        wallet1.WithdrawableBalance.Should().Be(0m);
        wallet2.GiftBalance.Should().Be(250m);
        wallet2.WithdrawableBalance.Should().Be(0m);

        _recordedLedger.Should().HaveCount(4);
        _recordedLedger.Sum(e => e.Amount).Should().Be(0m);
        _recordedLedger.Should().OnlyContain(e => e.BalanceType == LedgerBalanceType.Gift);
        _recordedLedger.Should().OnlyContain(e => e.ReferenceType == LedgerReferenceType.AdminGrant);

        result.Response.Recipients.Should().AllSatisfy(r => r.Status.Should().Be("Granted"));
        _storedBatch!.Recipients.Should().AllSatisfy(r => r.Status.Should().Be(GiftRecipientStatus.Granted));

        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Idempotency_SamePayload_ReturnsSameResponseWithoutDuplicateGrant()
    {
        var handler = CreateHandler();
        var user = User.CreateFromStrapi("usr-1", confirmed: true, blocked: false);
        _userProvisioning.Setup(x => x.EnsureUserFromExternalAsync("usr-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProvisioningResult(user, Created: false));

        var wallet = Wallet.Create(Account.CreateUserAccount(user.Id).Id, user.Id);
        _provisioning.Setup(x => x.GetOrCreateUserWalletAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(Account.CreateUserAccount(user.Id), wallet));

        var command = new AdminGrantGiftNoorCommand(["usr-1"], 100m, "تکرار پاداش", "idemp-key-repeat", Guid.NewGuid());

        var firstResult = await handler.Handle(command, CancellationToken.None);
        firstResult.IsSuccess.Should().BeTrue();
        wallet.GiftBalance.Should().Be(100m);

        // اجرای دوباره با همان کلید و پارامترها
        var secondResult = await handler.Handle(command, CancellationToken.None);

        secondResult.IsSuccess.Should().BeTrue();
        secondResult.Response!.BatchId.Should().Be(firstResult.Response!.BatchId);
        secondResult.Response.TotalAmountNoor.Should().Be(100m);
        wallet.GiftBalance.Should().Be(100m); // مانده نباید دوباره افزایش یابد
        _recordedLedger.Should().HaveCount(2); // تعداد ردیف‌های لجر نباید اضافه شوند
    }

    [Fact]
    public async Task Idempotency_DifferentPayload_ReturnsConflict()
    {
        var handler = CreateHandler();
        var user = User.CreateFromStrapi("usr-1", confirmed: true, blocked: false);
        _userProvisioning.Setup(x => x.EnsureUserFromExternalAsync("usr-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProvisioningResult(user, Created: false));

        var wallet = Wallet.Create(Account.CreateUserAccount(user.Id).Id, user.Id);
        _provisioning.Setup(x => x.GetOrCreateUserWalletAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(Account.CreateUserAccount(user.Id), wallet));

        var command1 = new AdminGrantGiftNoorCommand(["usr-1"], 100m, "علت اول", "conflict-key", Guid.NewGuid());
        var command2 = new AdminGrantGiftNoorCommand(["usr-1"], 200m, "علت دوم متفاوت", "conflict-key", Guid.NewGuid());

        var firstResult = await handler.Handle(command1, CancellationToken.None);
        firstResult.IsSuccess.Should().BeTrue();

        var conflictResult = await handler.Handle(command2, CancellationToken.None);
        conflictResult.IsSuccess.Should().BeFalse();
        conflictResult.IsConflict.Should().BeTrue();
        conflictResult.ErrorMessage.Should().Be(ApplicationErrors.TopUpRequest_IdempotencyConflict);
    }

    [Fact]
    public async Task AllOrNothing_WhenOneUserIsBlocked_NoGrantIsRecorded()
    {
        var handler = CreateHandler();
        var activeUser = User.CreateFromStrapi("usr-active", confirmed: true, blocked: false);
        var blockedUser = User.CreateFromStrapi("usr-blocked", confirmed: true, blocked: true);

        _userProvisioning.Setup(x => x.EnsureUserFromExternalAsync("usr-active", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProvisioningResult(activeUser, Created: false));
        _userProvisioning.Setup(x => x.EnsureUserFromExternalAsync("usr-blocked", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProvisioningResult(blockedUser, Created: false));

        var activeWallet = Wallet.Create(Account.CreateUserAccount(activeUser.Id).Id, activeUser.Id);
        _provisioning.Setup(x => x.GetOrCreateUserWalletAsync(activeUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(Account.CreateUserAccount(activeUser.Id), activeWallet));

        var command = new AdminGrantGiftNoorCommand(["usr-active", "usr-blocked"], 500m, "پاداش", "key-all-or-nothing", Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.HasInvalidUsers.Should().BeTrue();
        result.InvalidUsers.Should().ContainSingle(u => u.ExternalUserId == "usr-blocked" && u.Reason == ApplicationErrors.Gift_UserBlocked);

        activeWallet.GiftBalance.Should().Be(0m);
        _recordedLedger.Should().BeEmpty();
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MissingUser_IsProvisionedBeforeGrantingGift()
    {
        var handler = CreateHandler();
        var syncedUser = User.CreateFromStrapi("usr-missing-initially", confirmed: true, blocked: false);

        _userProvisioning.Setup(x => x.EnsureUserFromExternalAsync("usr-missing-initially", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProvisioningResult(syncedUser, Created: true));

        var wallet = Wallet.Create(Account.CreateUserAccount(syncedUser.Id).Id, syncedUser.Id);
        _provisioning.Setup(x => x.GetOrCreateUserWalletAsync(syncedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(Account.CreateUserAccount(syncedUser.Id), wallet));

        var command = new AdminGrantGiftNoorCommand(["usr-missing-initially"], 300m, "کاربر تازه سینک شده", "key-sync", Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        wallet.GiftBalance.Should().Be(300m);
        _userProvisioning.Verify(
            x => x.EnsureUserFromExternalAsync("usr-missing-initially", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Reconciliation_BothWalletBalancesCanBeReconstructedFromLedgerEntriesOfSameType()
    {
        var handler = CreateHandler();
        var user = User.CreateFromStrapi("usr-recon", confirmed: true, blocked: false);
        _userProvisioning.Setup(x => x.EnsureUserFromExternalAsync("usr-recon", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProvisioningResult(user, Created: false));

        var userAccount = Account.CreateUserAccount(user.Id);
        var wallet = Wallet.Create(userAccount.Id, user.Id);

        // شبیه‌سازی شارژ عادی قبلی
        wallet.ApplyCredit(700m);
        _recordedLedger.Add(LedgerEntry.Create(userAccount.Id, 700m, Guid.NewGuid(), LedgerReferenceType.TopUp, balanceType: LedgerBalanceType.Regular));
        _recordedLedger.Add(LedgerEntry.Create(_systemAccount.Id, -700m, Guid.NewGuid(), LedgerReferenceType.TopUp, balanceType: LedgerBalanceType.Regular));

        _provisioning.Setup(x => x.GetOrCreateUserWalletAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(userAccount, wallet));

        // اعطای نور هدیه
        var command = new AdminGrantGiftNoorCommand(["usr-recon"], 400m, "هدیه تطبیق", "key-recon", Guid.NewGuid());
        var result = await handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();

        // بررسی تطبیق مانده‌های کیف‌پول بر اساس جمع ردیف‌های لجر هر نوع
        var userRegularLedgerSum = _recordedLedger
            .Where(e => e.AccountId == userAccount.Id && e.BalanceType == LedgerBalanceType.Regular)
            .Sum(e => e.Amount);

        var userGiftLedgerSum = _recordedLedger
            .Where(e => e.AccountId == userAccount.Id && e.BalanceType == LedgerBalanceType.Gift)
            .Sum(e => e.Amount);

        userRegularLedgerSum.Should().Be(wallet.WithdrawableBalance);
        userGiftLedgerSum.Should().Be(wallet.GiftBalance);
        wallet.WithdrawableBalance.Should().Be(700m);
        wallet.GiftBalance.Should().Be(400m);
        wallet.TotalBalance.Should().Be(1100m);
    }

    [Fact]
    public void CurrentFlows_CannotDebitGiftBalance_FailsWhenRegularIsZeroAndGiftIs100()
    {
        var userAccount = Account.CreateUserAccount(UserId.New());
        var wallet = Wallet.Create(userAccount.Id, UserId.New());

        // مانده عادی صفر است و ۱۰۰ نور هدیه داده می‌شود
        wallet.ApplyGiftCredit(100m);

        wallet.WithdrawableBalance.Should().Be(0m);
        wallet.GiftBalance.Should().Be(100m);
        wallet.TotalBalance.Should().Be(100m);

        // تلاش برای کسر موجودی در مسیرهای فعلی کسر
        var act = () => wallet.ApplyDebit(10m);

        act.Should().Throw<DomainException>()
            .WithMessage(DomainErrors.Wallet_InsufficientBalance);

        // مانده هدیه دست‌نخورده باقی می‌ماند
        wallet.GiftBalance.Should().Be(100m);
        wallet.WithdrawableBalance.Should().Be(0m);
    }

    [Fact]
    public async Task Validator_RejectsDuplicateUserIds_NonPositiveIntegers_AndLimitExceeded()
    {
        var validator = new AdminGrantGiftNoorCommandValidator(new GiftSettings
        {
            MaxUsersPerBatch = 2
        });

        // شناسه تکراری
        var duplicateResult = await validator.ValidateAsync(new AdminGrantGiftNoorCommand(["usr-1", "usr-1"], 100m, "دلیل", "key-dup", null));
        duplicateResult.IsValid.Should().BeFalse();
        duplicateResult.Errors.Should().Contain(e => e.ErrorMessage == ApplicationErrors.Gift_DuplicateUsersNotAllowed);

        // عدد اعشاری
        var decimalResult = await validator.ValidateAsync(new AdminGrantGiftNoorCommand(["usr-1"], 100.5m, "دلیل", "key-dec", null));
        decimalResult.IsValid.Should().BeFalse();
        decimalResult.Errors.Should().Contain(e => e.ErrorMessage == ApplicationErrors.Gift_AmountMustBePositiveInteger);

        // مبلغ صفر یا منفی
        var negativeResult = await validator.ValidateAsync(new AdminGrantGiftNoorCommand(["usr-1"], -10m, "دلیل", "key-neg", null));
        negativeResult.IsValid.Should().BeFalse();

        // بیش از سقف کاربر
        var usersLimitResult = await validator.ValidateAsync(new AdminGrantGiftNoorCommand(["u1", "u2", "u3"], 100m, "دلیل", "key-users", null));
        usersLimitResult.IsValid.Should().BeFalse();
        usersLimitResult.Errors.Should().Contain(e => e.ErrorMessage == string.Format(ApplicationErrors.Gift_UsersExceedsLimit, 2));

        // مبالغ بزرگ مجاز هستند و سقف مبلغ وجود ندارد
        var largeAmountResult = await validator.ValidateAsync(new AdminGrantGiftNoorCommand(["u1", "u2"], 10_000_000m, "علت موجه", "key-large", null));
        largeAmountResult.IsValid.Should().BeTrue();

        // درخواست معتبر
        var validResult = await validator.ValidateAsync(new AdminGrantGiftNoorCommand(["u1", "u2"], 500m, "علت موجه", "key-valid", null));
        validResult.IsValid.Should().BeTrue();
    }
}

