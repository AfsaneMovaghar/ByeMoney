using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Commands.SyncUserFromStrapi;
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
using MediatR;
using Moq;

namespace ByeMoney.UnitTests;

public sealed class AdminGiftNoorTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IWalletRepository> _walletRepo = new();
    private readonly Mock<IGiftGrantRepository> _giftGrantRepo = new();
    private readonly Mock<IUserWalletProvisioningService> _provisioning = new();
    private readonly Mock<IRepository<LedgerEntry, LedgerEntryId>> _ledgerRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ISender> _sender = new();

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

        return new AdminGrantGiftNoorCommandHandler(
            _userRepo.Object,
            _giftGrantRepo.Object,
            _provisioning.Object,
            new ByeMoney.Application.Modules.Wallet.Services.WalletPostingService(_ledgerRepo.Object, _walletRepo.Object),
            _unitOfWork.Object,
            _sender.Object);
    }

    [Fact]
    public async Task Success_GrantsGift_RecordsBalancedLedger_AndIncreasesGiftBalance()
    {
        var handler = CreateHandler();
        var user1 = User.CreateFromStrapi("usr-1", confirmed: true, blocked: false);
        var user2 = User.CreateFromStrapi("usr-2", confirmed: true, blocked: false);

        _userRepo.Setup(x => x.GetByExternalUserIdAsync("usr-1", It.IsAny<CancellationToken>())).ReturnsAsync(user1);
        _userRepo.Setup(x => x.GetByExternalUserIdAsync("usr-2", It.IsAny<CancellationToken>())).ReturnsAsync(user2);

        var wallet1 = Wallet.Create(Account.CreateUserAccount(user1.Id).Id, user1.Id);
        var wallet2 = Wallet.Create(Account.CreateUserAccount(user2.Id).Id, user2.Id);

        _provisioning.Setup(x => x.GetOrCreateUserWalletAsync(user1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(Account.CreateUserAccount(user1.Id), wallet1));
        _provisioning.Setup(x => x.GetOrCreateUserWalletAsync(user2.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(Account.CreateUserAccount(user2.Id), wallet2));

        var command = new AdminGrantGiftNoorCommand(["usr-1", "usr-2"], 250m, "Ù‡Ø¯ÛŒÙ‡ Ù…Ø³Ø§Ø¨Ù‚Ù‡", "key-1", Guid.NewGuid());

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
        _userRepo.Setup(x => x.GetByExternalUserIdAsync("usr-1", It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var wallet = Wallet.Create(Account.CreateUserAccount(user.Id).Id, user.Id);
        _provisioning.Setup(x => x.GetOrCreateUserWalletAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(Account.CreateUserAccount(user.Id), wallet));

        var command = new AdminGrantGiftNoorCommand(["usr-1"], 100m, "ØªÚ©Ø±Ø§Ø± Ù¾Ø§Ø¯Ø§Ø´", "idemp-key-repeat", Guid.NewGuid());

        var firstResult = await handler.Handle(command, CancellationToken.None);
        firstResult.IsSuccess.Should().BeTrue();
        wallet.GiftBalance.Should().Be(100m);

        // Ø§Ø¬Ø±Ø§ÛŒ Ø¯ÙˆØ¨Ø§Ø±Ù‡ Ø¨Ø§ Ù‡Ù…Ø§Ù† Ú©Ù„ÛŒØ¯ Ùˆ Ù¾Ø§Ø±Ø§Ù…ØªØ±Ù‡Ø§
        var secondResult = await handler.Handle(command, CancellationToken.None);

        secondResult.IsSuccess.Should().BeTrue();
        secondResult.Response!.BatchId.Should().Be(firstResult.Response!.BatchId);
        secondResult.Response.TotalAmountNoor.Should().Be(100m);
        wallet.GiftBalance.Should().Be(100m); // Ù…Ø§Ù†Ø¯Ù‡ Ù†Ø¨Ø§ÛŒØ¯ Ø¯ÙˆØ¨Ø§Ø±Ù‡ Ø§ÙØ²Ø§ÛŒØ´ ÛŒØ§Ø¨Ø¯
        _recordedLedger.Should().HaveCount(2); // ØªØ¹Ø¯Ø§Ø¯ Ø±Ø¯ÛŒÙâ€ŒÙ‡Ø§ÛŒ Ù„Ø¬Ø± Ù†Ø¨Ø§ÛŒØ¯ Ø§Ø¶Ø§ÙÙ‡ Ø´ÙˆÙ†Ø¯
    }

    [Fact]
    public async Task Idempotency_DifferentPayload_ReturnsConflict()
    {
        var handler = CreateHandler();
        var user = User.CreateFromStrapi("usr-1", confirmed: true, blocked: false);
        _userRepo.Setup(x => x.GetByExternalUserIdAsync("usr-1", It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var wallet = Wallet.Create(Account.CreateUserAccount(user.Id).Id, user.Id);
        _provisioning.Setup(x => x.GetOrCreateUserWalletAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(Account.CreateUserAccount(user.Id), wallet));

        var command1 = new AdminGrantGiftNoorCommand(["usr-1"], 100m, "Ø¹Ù„Øª Ø§ÙˆÙ„", "conflict-key", Guid.NewGuid());
        var command2 = new AdminGrantGiftNoorCommand(["usr-1"], 200m, "Ø¹Ù„Øª Ø¯ÙˆÙ… Ù…ØªÙØ§ÙˆØª", "conflict-key", Guid.NewGuid());

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

        _userRepo.Setup(x => x.GetByExternalUserIdAsync("usr-active", It.IsAny<CancellationToken>())).ReturnsAsync(activeUser);
        _userRepo.Setup(x => x.GetByExternalUserIdAsync("usr-blocked", It.IsAny<CancellationToken>())).ReturnsAsync(blockedUser);

        var activeWallet = Wallet.Create(Account.CreateUserAccount(activeUser.Id).Id, activeUser.Id);
        _provisioning.Setup(x => x.GetOrCreateUserWalletAsync(activeUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(Account.CreateUserAccount(activeUser.Id), activeWallet));

        var command = new AdminGrantGiftNoorCommand(["usr-active", "usr-blocked"], 500m, "Ù¾Ø§Ø¯Ø§Ø´", "key-all-or-nothing", Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.HasInvalidUsers.Should().BeTrue();
        result.InvalidUsers.Should().ContainSingle(u => u.ExternalUserId == "usr-blocked" && u.Reason == ApplicationErrors.Gift_UserBlocked);

        activeWallet.GiftBalance.Should().Be(0m);
        _recordedLedger.Should().BeEmpty();
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncMissingUser_CallsSyncCommandAndGrantsGiftIfValid()
    {
        var handler = CreateHandler();
        User? nullUserInitially = null;
        var syncedUser = User.CreateFromStrapi("usr-missing-initially", confirmed: true, blocked: false);

        _userRepo.SetupSequence(x => x.GetByExternalUserIdAsync("usr-missing-initially", It.IsAny<CancellationToken>()))
            .ReturnsAsync(nullUserInitially)
            .ReturnsAsync(syncedUser);

        _sender.Setup(x => x.Send(It.IsAny<SyncUserFromStrapiCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(syncedUser.Id.Value);

        var wallet = Wallet.Create(Account.CreateUserAccount(syncedUser.Id).Id, syncedUser.Id);
        _provisioning.Setup(x => x.GetOrCreateUserWalletAsync(syncedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(Account.CreateUserAccount(syncedUser.Id), wallet));

        var command = new AdminGrantGiftNoorCommand(["usr-missing-initially"], 300m, "Ú©Ø§Ø±Ø¨Ø± ØªØ§Ø²Ù‡ Ø³ÛŒÙ†Ú© Ø´Ø¯Ù‡", "key-sync", Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        wallet.GiftBalance.Should().Be(300m);
        _sender.Verify(x => x.Send(It.Is<SyncUserFromStrapiCommand>(c => c.ExternalUserId == "usr-missing-initially"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reconciliation_BothWalletBalancesCanBeReconstructedFromLedgerEntriesOfSameType()
    {
        var handler = CreateHandler();
        var user = User.CreateFromStrapi("usr-recon", confirmed: true, blocked: false);
        _userRepo.Setup(x => x.GetByExternalUserIdAsync("usr-recon", It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var userAccount = Account.CreateUserAccount(user.Id);
        var wallet = Wallet.Create(userAccount.Id, user.Id);

        // Ø´Ø¨ÛŒÙ‡â€ŒØ³Ø§Ø²ÛŒ Ø´Ø§Ø±Ú˜ Ø¹Ø§Ø¯ÛŒ Ù‚Ø¨Ù„ÛŒ
        wallet.ApplyCredit(700m);
        _recordedLedger.Add(LedgerEntry.Create(userAccount.Id, 700m, Guid.NewGuid(), LedgerReferenceType.TopUp, balanceType: LedgerBalanceType.Regular));
        _recordedLedger.Add(LedgerEntry.Create(_systemAccount.Id, -700m, Guid.NewGuid(), LedgerReferenceType.TopUp, balanceType: LedgerBalanceType.Regular));

        _provisioning.Setup(x => x.GetOrCreateUserWalletAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionedUserWallet(userAccount, wallet));

        // Ø§Ø¹Ø·Ø§ÛŒ Ù†ÙˆØ± Ù‡Ø¯ÛŒÙ‡
        var command = new AdminGrantGiftNoorCommand(["usr-recon"], 400m, "Ù‡Ø¯ÛŒÙ‡ ØªØ·Ø¨ÛŒÙ‚", "key-recon", Guid.NewGuid());
        var result = await handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();

        // Ø¨Ø±Ø±Ø³ÛŒ ØªØ·Ø¨ÛŒÙ‚ Ù…Ø§Ù†Ø¯Ù‡â€ŒÙ‡Ø§ÛŒ Ú©ÛŒÙâ€ŒÙ¾ÙˆÙ„ Ø¨Ø± Ø§Ø³Ø§Ø³ Ø¬Ù…Ø¹ Ø±Ø¯ÛŒÙâ€ŒÙ‡Ø§ÛŒ Ù„Ø¬Ø± Ù‡Ø± Ù†ÙˆØ¹
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

        // Ù…Ø§Ù†Ø¯Ù‡ Ø¹Ø§Ø¯ÛŒ ØµÙØ± Ø§Ø³Øª Ùˆ Û±Û°Û° Ù†ÙˆØ± Ù‡Ø¯ÛŒÙ‡ Ø¯Ø§Ø¯Ù‡ Ù…ÛŒâ€ŒØ´ÙˆØ¯
        wallet.ApplyGiftCredit(100m);

        wallet.WithdrawableBalance.Should().Be(0m);
        wallet.GiftBalance.Should().Be(100m);
        wallet.TotalBalance.Should().Be(100m);

        // ØªÙ„Ø§Ø´ Ø¨Ø±Ø§ÛŒ Ú©Ø³Ø± Ù…ÙˆØ¬ÙˆØ¯ÛŒ Ø¯Ø± Ù…Ø³ÛŒØ±Ù‡Ø§ÛŒ ÙØ¹Ù„ÛŒ Ú©Ø³Ø±
        var act = () => wallet.ApplyDebit(10m);

        act.Should().Throw<DomainException>()
            .WithMessage(DomainErrors.Wallet_InsufficientBalance);

        // Ù…Ø§Ù†Ø¯Ù‡ Ù‡Ø¯ÛŒÙ‡ Ø¯Ø³Øªâ€ŒÙ†Ø®ÙˆØ±Ø¯Ù‡ Ø¨Ø§Ù‚ÛŒ Ù…ÛŒâ€ŒÙ…Ø§Ù†Ø¯
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

        // Ø´Ù†Ø§Ø³Ù‡ ØªÚ©Ø±Ø§Ø±ÛŒ
        var duplicateResult = await validator.ValidateAsync(new AdminGrantGiftNoorCommand(["usr-1", "usr-1"], 100m, "Ø¯Ù„ÛŒÙ„", "key-dup", null));
        duplicateResult.IsValid.Should().BeFalse();
        duplicateResult.Errors.Should().Contain(e => e.ErrorMessage == ApplicationErrors.Gift_DuplicateUsersNotAllowed);

        // Ø¹Ø¯Ø¯ Ø§Ø¹Ø´Ø§Ø±ÛŒ
        var decimalResult = await validator.ValidateAsync(new AdminGrantGiftNoorCommand(["usr-1"], 100.5m, "Ø¯Ù„ÛŒÙ„", "key-dec", null));
        decimalResult.IsValid.Should().BeFalse();
        decimalResult.Errors.Should().Contain(e => e.ErrorMessage == ApplicationErrors.Gift_AmountMustBePositiveInteger);

        // Ù…Ø¨Ù„Øº ØµÙØ± ÛŒØ§ Ù…Ù†ÙÛŒ
        var negativeResult = await validator.ValidateAsync(new AdminGrantGiftNoorCommand(["usr-1"], -10m, "Ø¯Ù„ÛŒÙ„", "key-neg", null));
        negativeResult.IsValid.Should().BeFalse();

        // Ø¨ÛŒØ´ Ø§Ø² Ø³Ù‚Ù Ú©Ø§Ø±Ø¨Ø±
        var usersLimitResult = await validator.ValidateAsync(new AdminGrantGiftNoorCommand(["u1", "u2", "u3"], 100m, "Ø¯Ù„ÛŒÙ„", "key-users", null));
        usersLimitResult.IsValid.Should().BeFalse();
        usersLimitResult.Errors.Should().Contain(e => e.ErrorMessage == string.Format(ApplicationErrors.Gift_UsersExceedsLimit, 2));

        // Ù…Ø¨Ø§Ù„Øº Ø¨Ø²Ø±Ú¯ Ù…Ø¬Ø§Ø² Ù‡Ø³ØªÙ†Ø¯ Ùˆ Ø³Ù‚Ù Ù…Ø¨Ù„Øº ÙˆØ¬ÙˆØ¯ Ù†Ø¯Ø§Ø±Ø¯
        var largeAmountResult = await validator.ValidateAsync(new AdminGrantGiftNoorCommand(["u1", "u2"], 10_000_000m, "Ø¹Ù„Øª Ù…ÙˆØ¬Ù‡", "key-large", null));
        largeAmountResult.IsValid.Should().BeTrue();

        // Ø¯Ø±Ø®ÙˆØ§Ø³Øª Ù…Ø¹ØªØ¨Ø±
        var validResult = await validator.ValidateAsync(new AdminGrantGiftNoorCommand(["u1", "u2"], 500m, "Ø¹Ù„Øª Ù…ÙˆØ¬Ù‡", "key-valid", null));
        validResult.IsValid.Should().BeTrue();
    }
}

