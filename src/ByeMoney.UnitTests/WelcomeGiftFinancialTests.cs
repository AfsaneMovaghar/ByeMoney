using ByeMoney.Application;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Commands.SyncUserFromStrapi;
using ByeMoney.Application.Modules.Identity.Users.Interfaces;
using ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Wallet.Services;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Gifts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using ByeMoney.Infrastructure;
using ByeMoney.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace ByeMoney.UnitTests;

public sealed class WelcomeGiftFinancialTests : IAsyncLifetime
{
    private readonly string _schema = "welcome_gift_test_" + Guid.NewGuid().ToString("N");
    private ServiceProvider? _provider;
    private string? _adminConnection;

    public async Task InitializeAsync()
    {
        var testConn = Environment.GetEnvironmentVariable("BYEMONEY_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(testConn) && Environment.GetEnvironmentVariable("BYEMONEY_CART_PG_TESTS") != "1")
            return;

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ByeMoney.sln")))
            directory = directory.Parent;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(directory!.FullName, "src", "ByeMoney.API"))
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        _adminConnection = !string.IsNullOrWhiteSpace(testConn)
            ? testConn
            : configuration.GetConnectionString("DefaultConnection")!;

        await using (var connection = new NpgsqlConnection(_adminConnection))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE SCHEMA \"{_schema}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(_adminConnection) { SearchPath = _schema };
        var services = new ServiceCollection()
            .AddLogging()
            .AddApplicationServices()
            .AddInfrastructureServices(configuration);

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(builder.ConnectionString,
                postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", _schema)));

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_provider is null) return;
        await _provider.DisposeAsync();

        if (!string.IsNullOrWhiteSpace(_adminConnection))
        {
            await using var connection = new NpgsqlConnection(_adminConnection);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE", connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    // ۱. هدیه فعال: افزایش کیف پول به میزان ۵۰ نور و ثبت متوازن سطرهای دفتر کل و ذی‌نفع هدیه
    [Fact]
    public async Task WelcomeGift_WhenCampaignActive_GrantsGift_IncreasesWallet_AndRecordsBalancedLedger()
    {
        var userRepoMock = new Mock<IUserRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var tarhElahiMock = new Mock<ITarhElahiIntegrationClient>();
        var loggerMock = new Mock<ILogger<SyncUserFromStrapiCommandHandler>>();
        var giftSettingRepoMock = new Mock<IGiftCampaignSettingRepository>();
        var giftGrantRepoMock = new Mock<IGiftGrantRepository>();
        var provisioningMock = new Mock<IUserWalletProvisioningService>();
        var noorCreditMock = new Mock<INoorCreditService>();

        var externalUserId = "strapi-welcome-active-1";
        tarhElahiMock.Setup(x => x.GetUserAsync(externalUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TarhElahiUserDto
            {
                ExternalUserId = externalUserId,
                PhoneNumber = "09121111111",
                Email = "w1@test.com",
                FirstName = "Ali",
                LastName = "Rezaei",
                Confirmed = true,
                Blocked = false
            });

        userRepoMock.Setup(x => x.GetByExternalUserIdAsync(externalUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var activeSetting = GiftCampaignSetting.Create(GiftCampaignType.Welcome, 50m, isEnabled: true);
        giftSettingRepoMock.Setup(x => x.GetByTypeAsync(GiftCampaignType.Welcome, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeSetting);

        GiftGrantBatch? capturedBatch = null;
        giftGrantRepoMock.Setup(x => x.AddBatchAsync(It.IsAny<GiftGrantBatch>(), It.IsAny<CancellationToken>()))
            .Callback<GiftGrantBatch, CancellationToken>((batch, _) => capturedBatch = batch)
            .Returns(Task.CompletedTask);

        var txId = Guid.NewGuid();
        noorCreditMock.Setup(x => x.CreditAsync(It.IsAny<NoorCreditRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NoorCreditRequest req, CancellationToken _) =>
                new NoorCreditResult(txId, req.AmountNoor, 0m, req.AmountNoor, null!));

        var welcomeGiftService = new WelcomeGiftService(
            giftSettingRepoMock.Object,
            giftGrantRepoMock.Object,
            noorCreditMock.Object,
            provisioningMock.Object);

        var handler = new SyncUserFromStrapiCommandHandler(
            userRepoMock.Object,
            unitOfWorkMock.Object,
            tarhElahiMock.Object,
            loggerMock.Object,
            welcomeGiftService);

        // Act
        var resultUserId = await handler.Handle(new SyncUserFromStrapiCommand(externalUserId), CancellationToken.None);

        // Assert
        resultUserId.Should().NotBeEmpty();
        userRepoMock.Verify(x => x.AddAsync(It.Is<User>(u => u.ExternalUserId == externalUserId), It.IsAny<CancellationToken>()), Times.Once);
        noorCreditMock.Verify(x => x.CreditAsync(It.Is<NoorCreditRequest>(r =>
            r.AmountNoor == 50m &&
            r.BalanceType == LedgerBalanceType.Gift &&
            r.ReferenceType == LedgerReferenceType.WelcomeGift), It.IsAny<CancellationToken>()), Times.Once);

        capturedBatch.Should().NotBeNull();
        capturedBatch!.Source.Should().Be(GiftGrantSource.WelcomeBonus);
        capturedBatch.AmountNoorPerUser.Should().Be(50m);
        capturedBatch.Recipients.Should().HaveCount(1);
        capturedBatch.RequestPayloadHash.Should().NotBeNullOrWhiteSpace();
        capturedBatch.RequestPayloadHash.Should().NotBe(capturedBatch.IdempotencyKey);
        unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ۲. هدیه غیرفعال: کیف پول صفر بدون سطر هدیه، بچ و دفتر کل ثبت می‌شود
    [Fact]
    public async Task WelcomeGift_WhenCampaignDisabled_CreatesUserAndWallet_WithoutGift()
    {
        var userRepoMock = new Mock<IUserRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var tarhElahiMock = new Mock<ITarhElahiIntegrationClient>();
        var loggerMock = new Mock<ILogger<SyncUserFromStrapiCommandHandler>>();
        var giftSettingRepoMock = new Mock<IGiftCampaignSettingRepository>();
        var giftGrantRepoMock = new Mock<IGiftGrantRepository>();
        var provisioningMock = new Mock<IUserWalletProvisioningService>();
        var noorCreditMock = new Mock<INoorCreditService>();

        var externalUserId = "strapi-welcome-disabled-1";
        tarhElahiMock.Setup(x => x.GetUserAsync(externalUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TarhElahiUserDto
            {
                ExternalUserId = externalUserId,
                PhoneNumber = "09122222222",
                Email = "w2@test.com",
                FirstName = "Sara",
                LastName = "Ahmadi",
                Confirmed = true,
                Blocked = false
            });

        userRepoMock.Setup(x => x.GetByExternalUserIdAsync(externalUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var disabledSetting = GiftCampaignSetting.Create(GiftCampaignType.Welcome, 50m, isEnabled: false);
        giftSettingRepoMock.Setup(x => x.GetByTypeAsync(GiftCampaignType.Welcome, It.IsAny<CancellationToken>()))
            .ReturnsAsync(disabledSetting);

        var welcomeGiftService = new WelcomeGiftService(
            giftSettingRepoMock.Object,
            giftGrantRepoMock.Object,
            noorCreditMock.Object,
            provisioningMock.Object);

        var handler = new SyncUserFromStrapiCommandHandler(
            userRepoMock.Object,
            unitOfWorkMock.Object,
            tarhElahiMock.Object,
            loggerMock.Object,
            welcomeGiftService);

        // Act
        var resultUserId = await handler.Handle(new SyncUserFromStrapiCommand(externalUserId), CancellationToken.None);

        // Assert
        resultUserId.Should().NotBeEmpty();
        userRepoMock.Verify(x => x.AddAsync(It.Is<User>(u => u.ExternalUserId == externalUserId), It.IsAny<CancellationToken>()), Times.Once);
        provisioningMock.Verify(x => x.GetOrCreateUserWalletAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()), Times.Once);
        noorCreditMock.Verify(x => x.CreditAsync(It.IsAny<NoorCreditRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        giftGrantRepoMock.Verify(x => x.AddBatchAsync(It.IsAny<GiftGrantBatch>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ۳. خارج از بازه زمانی هدیه: ورود خارج از بازه تاریخ شروع و پایان هدیه نمی‌گیرد
    [Fact]
    public async Task WelcomeGift_WhenOutsideDateRange_CreatesUserAndWallet_WithoutGift()
    {
        var userRepoMock = new Mock<IUserRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var tarhElahiMock = new Mock<ITarhElahiIntegrationClient>();
        var loggerMock = new Mock<ILogger<SyncUserFromStrapiCommandHandler>>();
        var giftSettingRepoMock = new Mock<IGiftCampaignSettingRepository>();
        var giftGrantRepoMock = new Mock<IGiftGrantRepository>();
        var provisioningMock = new Mock<IUserWalletProvisioningService>();
        var noorCreditMock = new Mock<INoorCreditService>();

        var externalUserId = "strapi-welcome-expired-1";
        tarhElahiMock.Setup(x => x.GetUserAsync(externalUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TarhElahiUserDto
            {
                ExternalUserId = externalUserId,
                PhoneNumber = "09123333333",
                Email = "w3@test.com",
                FirstName = "Omid",
                LastName = "Kiani",
                Confirmed = true,
                Blocked = false
            });

        userRepoMock.Setup(x => x.GetByExternalUserIdAsync(externalUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // کمپین منقضی شده در گذشته
        var expiredSetting = GiftCampaignSetting.Create(
            GiftCampaignType.Welcome,
            50m,
            isEnabled: true,
            startDateUtc: DateTime.UtcNow.AddMonths(-2),
            endDateUtc: DateTime.UtcNow.AddMonths(-1));

        giftSettingRepoMock.Setup(x => x.GetByTypeAsync(GiftCampaignType.Welcome, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredSetting);

        var welcomeGiftService = new WelcomeGiftService(
            giftSettingRepoMock.Object,
            giftGrantRepoMock.Object,
            noorCreditMock.Object,
            provisioningMock.Object);

        var handler = new SyncUserFromStrapiCommandHandler(
            userRepoMock.Object,
            unitOfWorkMock.Object,
            tarhElahiMock.Object,
            loggerMock.Object,
            welcomeGiftService);

        // Act
        var resultUserId = await handler.Handle(new SyncUserFromStrapiCommand(externalUserId), CancellationToken.None);

        // Assert
        resultUserId.Should().NotBeEmpty();
        noorCreditMock.Verify(x => x.CreditAsync(It.IsAny<NoorCreditRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        provisioningMock.Verify(x => x.GetOrCreateUserWalletAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ۴. آزمون همزمانی روی PostgreSQL مستقل: دو درخواست همزمان برای یک کاربر استرپی
    [PostgresFact]
    public async Task WelcomeGift_ConcurrentRequestsForSameUser_ExecutedOnPostgres_PreventsDuplicateCreation()
    {
        if (_provider is null) return;

        var externalUserId = "pg-concurrent-user-" + Guid.NewGuid().ToString("N");
        var tarhElahiMock = new Mock<ITarhElahiIntegrationClient>();
        tarhElahiMock.Setup(x => x.GetUserAsync(externalUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TarhElahiUserDto
            {
                ExternalUserId = externalUserId,
                PhoneNumber = "09124444444",
                Email = "conc@test.com",
                FirstName = "Reza",
                LastName = "Rad",
                Confirmed = true,
                Blocked = false
            });

        // ایجاد دو دامنه سرویس جداگانه متصل به دیتابیس مشترک پستگرس
        using var scope1 = _provider.CreateScope();
        using var scope2 = _provider.CreateScope();

        var db1 = scope1.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var handler1 = new SyncUserFromStrapiCommandHandler(
            new ByeMoney.Infrastructure.Modules.Identity.Persistence.User.UserRepository(db1),
            db1,
            tarhElahiMock.Object,
            scope1.ServiceProvider.GetRequiredService<ILogger<SyncUserFromStrapiCommandHandler>>(),
            scope1.ServiceProvider.GetRequiredService<IWelcomeGiftService>());

        var handler2 = new SyncUserFromStrapiCommandHandler(
            new ByeMoney.Infrastructure.Modules.Identity.Persistence.User.UserRepository(db2),
            db2,
            tarhElahiMock.Object,
            scope2.ServiceProvider.GetRequiredService<ILogger<SyncUserFromStrapiCommandHandler>>(),
            scope2.ServiceProvider.GetRequiredService<IWelcomeGiftService>());

        var command = new SyncUserFromStrapiCommand(externalUserId);

        var task1 = Task.Run(async () =>
        {
            try { return await handler1.Handle(command, CancellationToken.None); }
            catch { return Guid.Empty; }
        });

        var task2 = Task.Run(async () =>
        {
            try { return await handler2.Handle(command, CancellationToken.None); }
            catch { return Guid.Empty; }
        });

        var results = await Task.WhenAll(task1, task2);

        // دقیقاً یکی از درخواست‌ها باید با موفقیت ثبت شود و دیگری با خطای ایندکس یکتا شکست بخورد
        results.Count(id => id != Guid.Empty).Should().Be(1);

        // در پایگاه داده بررسی شود که دقیقاً یک کاربر، یک کیف پول و یک رکورد هدیه وجود دارد
        using var verifyScope = _provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var createdUsers = await verifyDb.Set<User>().Where(u => u.ExternalUserId == externalUserId).ToListAsync();
        createdUsers.Should().HaveCount(1);

        var user = createdUsers.First();
        var wallet = await verifyDb.Set<Wallet>().SingleAsync(w => w.UserId == user.Id);
        wallet.GiftBalance.Should().Be(50m);

        var giftBatches = await verifyDb.Set<GiftGrantBatch>()
            .Include(b => b.Recipients)
            .Where(b => b.IdempotencyKey == $"welcome-bonus:{user.Id.Value}")
            .ToListAsync();

        giftBatches.Should().HaveCount(1);
        giftBatches.First().Recipients.Should().HaveCount(1);
    }

    // ۵. آزمون شکست در میانه روی PostgreSQL مستقل: رول‌بک اتمیک (نه کاربر بدون هدیه و نه هدیه بدون کاربر)
    [PostgresFact]
    public async Task WelcomeGift_MidwayFailure_ExecutedOnPostgres_RollsBackAllEntities()
    {
        if (_provider is null) return;

        var externalUserId = "pg-failure-user-" + Guid.NewGuid().ToString("N");
        var tarhElahiMock = new Mock<ITarhElahiIntegrationClient>();
        tarhElahiMock.Setup(x => x.GetUserAsync(externalUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TarhElahiUserDto
            {
                ExternalUserId = externalUserId,
                PhoneNumber = "09125555555",
                Email = "fail@test.com",
                FirstName = "Vahid",
                LastName = "Saberi",
                Confirmed = true,
                Blocked = false
            });

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // شبیه‌سازی کلاینت شکست‌خورده در تراکنش: لغو عملیات قبل از ذخیره نهایی
        using var cts = new CancellationTokenSource();

        var handler = new SyncUserFromStrapiCommandHandler(
            new ByeMoney.Infrastructure.Modules.Identity.Persistence.User.UserRepository(db),
            db,
            tarhElahiMock.Object,
            scope.ServiceProvider.GetRequiredService<ILogger<SyncUserFromStrapiCommandHandler>>(),
            scope.ServiceProvider.GetRequiredService<IWelcomeGiftService>());

        cts.Cancel(); // لغو توکن قبل از اتمام

        var act = async () => await handler.Handle(new SyncUserFromStrapiCommand(externalUserId), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();

        // اطمینان از پاک بودن کامل وضعیت دیتابیس (رول‌بک اتمیک)
        using var verifyScope = _provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var userExists = await verifyDb.Set<User>().AnyAsync(u => u.ExternalUserId == externalUserId);
        userExists.Should().BeFalse();

        var recipientExists = await verifyDb.Set<GiftGrantRecipient>().AnyAsync(r => r.ExternalUserId == externalUserId);
        recipientExists.Should().BeFalse();

        var ledgerExists = await verifyDb.Set<LedgerEntry>().AnyAsync(l => l.ReferenceType == LedgerReferenceType.WelcomeGift);
        ledgerExists.Should().BeFalse();
    }
}

