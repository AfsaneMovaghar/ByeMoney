using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using ByeMoney.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interfaces;
using ByeMoney.Application.Modules.Settings.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.CreateAdminCardToCardTopUp;
using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Wallet.Services;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Infrastructure.Modules.Wallet.Persistence.TopUp;
using ByeMoney.Infrastructure.Modules.Wallet.Persistence.Wallet;
using MediatR;
using Moq;

namespace ByeMoney.UnitTests;

public class WalletConcurrencyIntegrationTests
{
    [PostgresFact]
    public Task AssistedTopUpWithoutReceipt_ConcurrentReplayCreditsOnce()
    {
        return VerifyAssistedTopUpReplayAsync(null);
    }

    [PostgresFact]
    public Task AssistedTopUpWithReceipt_ConcurrentReplayCreditsOnce()
    {
        return VerifyAssistedTopUpReplayAsync("receipt-id");
    }

    private static async Task VerifyAssistedTopUpReplayAsync(string? receiptId)
    {
        await using var database = await TestDatabase.CreateAsync();
        var beneficiary = User.CreateFromStrapi("assisted-beneficiary", confirmed: true);
        var actor = User.CreateFromStrapi("assisted-actor", confirmed: true);
        var account = Account.CreateUserAccount(beneficiary.Id);
        var system = Account.CreateSystemAccount();
        var wallet = Wallet.Create(account.Id, beneficiary.Id);
        await using (var seed = database.Context())
        {
            seed.AddRange(beneficiary, actor, account, system, wallet);
            await seed.SaveChangesAsync();
        }

        var command = new CreateAdminCardToCardTopUpCommand(
            beneficiary.ExternalUserId, actor.Id.Value, 25_000m, receiptId, "same-key", null,
            ChargeType.AdminAssistedCardToCard);
        await using var first = database.Context();
        await using var second = database.Context();
        var bothRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readCount = 0;

        async Task<TopUpRequest?> ReadBeforeInsert(CancellationToken ct)
        {
            if (Interlocked.Increment(ref readCount) == 2)
            {
                bothRead.SetResult();
            }

            await bothRead.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            return null;
        }

        var outcomes = await Task.WhenAll(AttemptAsync(first), AttemptAsync(second));
        outcomes.Count(error => error is null).Should().Be(1);
        outcomes.OfType<DbUpdateException>().Should().ContainSingle()
            .Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);

        await using var replay = database.Context();
        var retry = await CreateService(replay, new TopUpRequestRepository(replay)).CreateAsync(command, default);
        retry.IsSuccess.Should().BeTrue();
        var conflict = await CreateService(replay, new TopUpRequestRepository(replay))
            .CreateAsync(command with { AmountToman = 50_000m }, default);
        conflict.Status.Should().Be(ByeMoney.Domain.Common.ResultStatus.Conflict);

        await using var check = database.Context();
        var topUp = await check.Set<TopUpRequest>().SingleAsync();
        topUp.Id.Value.Should().Be(retry.Value.TopUpRequestId);
        topUp.Status.Should().Be(TopUpStatus.Confirmed);
        topUp.ReceiptId.Should().Be(receiptId);
        topUp.RialPerNoorSnapshot.Should().Be(10_000m);
        (await check.Set<Wallet>().SingleAsync()).WithdrawableBalance.Should().Be(25m);
        var entries = await check.Set<LedgerEntry>().ToListAsync();
        entries.Should().HaveCount(2);
        entries.Sum(entry => entry.Amount).Should().Be(0m);
        entries.Select(entry => entry.TransactionId).Distinct().Should().ContainSingle();

        async Task<Exception?> AttemptAsync(ApplicationDbContext context)
        {
            var repository = new Mock<ITopUpRequestRepository>();
            repository.Setup(x => x.GetByIdempotencyKeyAsync(command.IdempotencyKey, It.IsAny<CancellationToken>()))
                .Returns((string _, CancellationToken ct) => ReadBeforeInsert(ct));
            repository.Setup(x => x.AddAsync(It.IsAny<TopUpRequest>(), It.IsAny<CancellationToken>()))
                .Returns((TopUpRequest topUp, CancellationToken ct) => new TopUpRequestRepository(context).AddAsync(topUp, ct));

            return await Xunit.Record.ExceptionAsync(async () =>
            {
                var result = await CreateService(context, repository.Object).CreateAsync(command, default);
                result.IsSuccess.Should().BeTrue();
            });
        }

        AdminAssistedTopUpService CreateService(ApplicationDbContext context, ITopUpRequestRepository repository)
        {
            var users = new Mock<IUserRepository>();
            users.Setup(x => x.GetByExternalUserIdAsync(beneficiary.ExternalUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(beneficiary);
            var settings = new Mock<ISystemSettingRepository>();
            settings.Setup(x => x.GetRialToNoorConversionRateAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(10_000m);
            var provisioning = new Mock<IUserWalletProvisioningService>();
            provisioning.Setup(x => x.GetOrCreateUserWalletAsync(beneficiary.Id, It.IsAny<CancellationToken>()))
                .Returns(async () => new ProvisionedUserWallet(account, await context.Set<Wallet>().SingleAsync()));
            provisioning.Setup(x => x.GetOrCreateSystemAccountAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(system);
            var settlement = new TopUpSettlementService(provisioning.Object, new WalletPostingService(new BaseRepository<LedgerEntry, LedgerEntryId>(context), new WalletRepository(context)), context);

            return new AdminAssistedTopUpService(users.Object, settings.Object, repository, settlement,
                Mock.Of<IReceiptStorage>(), Mock.Of<ISender>());
        }
    }

    [PostgresFact]
    public async Task IndependentCredits_PreserveBothBalancesAndBalancedLedger()
    {
        await using var database = await TestDatabase.CreateAsync();
        var id = await database.SeedWalletAsync();
        await using var first = database.Context();
        await using var second = database.Context();
        var wallet1 = await first.Set<Wallet>().SingleAsync(x => x.Id == id);
        var wallet2 = await second.Set<Wallet>().SingleAsync(x => x.Id == id);
        Record(first, wallet1, 10m);
        Record(second, wallet2, 20m);

        await Task.WhenAll(first.SaveChangesAsync(), second.SaveChangesAsync());

        await using var check = database.Context();
        (await check.Set<Wallet>().SingleAsync()).WithdrawableBalance.Should().Be(130m);
        var entries = await check.Set<LedgerEntry>().ToListAsync();
        entries.Should().HaveCount(6);
        entries.Where(x => x.AccountId == wallet1.AccountId).Sum(x => x.Amount).Should().Be(130m);
        entries.GroupBy(x => x.TransactionId).Should().OnlyContain(group => group.Count() == 2 && group.Sum(x => x.Amount) == 0);
    }

    [PostgresFact]
    public async Task StaleDebit_CannotOverspendOrLeaveUncommittedLedger()
    {
        await using var database = await TestDatabase.CreateAsync();
        var id = await database.SeedWalletAsync();
        await using var first = database.Context();
        await using var second = database.Context();
        var wallet1 = await first.Set<Wallet>().SingleAsync(x => x.Id == id);
        var wallet2 = await second.Set<Wallet>().SingleAsync(x => x.Id == id);
        Record(first, wallet1, -80m);
        Record(second, wallet2, -80m);
        await first.SaveChangesAsync();

        await FluentActions.Awaiting(() => second.SaveChangesAsync()).Should().ThrowAsync<Exception>();

        await using var check = database.Context();
        (await check.Set<Wallet>().SingleAsync()).WithdrawableBalance.Should().Be(20m);
        var entries = await check.Set<LedgerEntry>().ToListAsync();
        entries.Should().HaveCount(4);
        entries.Where(x => x.AccountId == wallet1.AccountId).Sum(x => x.Amount).Should().Be(20m);
        entries.Sum(x => x.Amount).Should().Be(0m);
    }

    [PostgresFact]
    public async Task ConcurrentGiftDebitsWithLock_PreserveBalancesAndBalancedLedger()
    {
        await using var database = await TestDatabase.CreateAsync();
        var accountId = AccountId.New();
        var userId = UserId.New();
        var systemAccount = Account.CreateSystemAccount();

        await using (var seed = database.Context())
        {
            var seedWallet = Wallet.Create(accountId, userId);
            seedWallet.Credit(100m, LedgerBalanceType.Gift);
            seedWallet.Credit(100m, LedgerBalanceType.Regular);
            seed.Add(systemAccount);
            seed.Add(seedWallet);
            await seed.SaveChangesAsync();
        }

        async Task ExecuteDebitWithLockAsync(decimal amount)
        {
            await using var context = database.Context();
            await using var tx = await context.Database.BeginTransactionAsync();
            var repo = new WalletRepository(context);
            var wallet = await repo.GetByUserIdForUpdateAsync(userId);
            wallet.Should().NotBeNull();

            var debitResult = wallet!.Debit(amount, WalletDebitPurpose.Purchase);
            var transactionId = Guid.NewGuid();

            if (debitResult.GiftDebited > 0)
            {
                context.Add(LedgerEntry.Create(wallet.AccountId, -debitResult.GiftDebited, transactionId, LedgerReferenceType.Purchase, balanceType: LedgerBalanceType.Gift));
                context.Add(LedgerEntry.Create(systemAccount.Id, debitResult.GiftDebited, transactionId, LedgerReferenceType.Purchase, balanceType: LedgerBalanceType.Gift));
            }

            if (debitResult.RegularDebited > 0)
            {
                context.Add(LedgerEntry.Create(wallet.AccountId, -debitResult.RegularDebited, transactionId, LedgerReferenceType.Purchase, balanceType: LedgerBalanceType.Regular));
                context.Add(LedgerEntry.Create(systemAccount.Id, debitResult.RegularDebited, transactionId, LedgerReferenceType.Purchase, balanceType: LedgerBalanceType.Regular));
            }

            await context.SaveChangesAsync();
            await tx.CommitAsync();
        }

        await Task.WhenAll(
            ExecuteDebitWithLockAsync(70m),
            ExecuteDebitWithLockAsync(50m)
        );

        await using var verify = database.Context();
        var finalWallet = await verify.Set<Wallet>().SingleAsync(w => w.UserId == userId);

        finalWallet.GiftBalance.Should().Be(0m);
        finalWallet.WithdrawableBalance.Should().Be(80m);
        finalWallet.TotalBalance.Should().Be(80m);

        var entries = await verify.Set<LedgerEntry>().ToListAsync();
        entries.Should().HaveCount(6);

        entries.GroupBy(e => e.TransactionId).Should().OnlyContain(group => group.Sum(e => e.Amount) == 0);

        var userEntries = entries.Where(e => e.AccountId == accountId).ToList();
        userEntries.Where(e => e.BalanceType == LedgerBalanceType.Gift).Sum(e => e.Amount).Should().Be(-100m);
        userEntries.Where(e => e.BalanceType == LedgerBalanceType.Regular).Sum(e => e.Amount).Should().Be(-20m);
        userEntries.Sum(e => e.Amount).Should().Be(-120m);
    }

    private static void Record(ApplicationDbContext context, Wallet wallet, decimal amount)
    {
        if (amount > 0) wallet.ApplyCredit(amount);
        else wallet.ApplyDebit(-amount);
        var transactionId = Guid.NewGuid();
        var referenceType = amount > 0 ? LedgerReferenceType.TopUp : LedgerReferenceType.Purchase;
        context.AddRange(
            LedgerEntry.Create(wallet.AccountId, amount, transactionId, referenceType, transactionId.ToString()),
            LedgerEntry.Create(new AccountId(Guid.Empty), -amount, transactionId, referenceType, transactionId.ToString()));
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string _schema = "wallet_test_" + Guid.NewGuid().ToString("N");
        private readonly NpgsqlConnection _connection;
        private readonly string _connectionString;

        private TestDatabase(string connectionString)
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = _schema };
            _connectionString = builder.ConnectionString;
            _connection = new NpgsqlConnection(_connectionString);
        }

        public static async Task<TestDatabase> CreateAsync()
        {
            var database = new TestDatabase(Environment.GetEnvironmentVariable("BYEMONEY_TEST_CONNECTION_STRING")!);
            await database._connection.OpenAsync();
            try
            {
                await using var create = new NpgsqlCommand($"CREATE SCHEMA {database._schema}", database._connection);
                await create.ExecuteNonQueryAsync();
                await using var context = database.Context();
                await context.Database.ExecuteSqlRawAsync(context.Database.GenerateCreateScript());
                return database;
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_connectionString).Options);

        public async Task<WalletId> SeedWalletAsync()
        {
            await using var context = Context();
            var wallet = Wallet.Create(AccountId.New(), UserId.New());
            context.Add(wallet);
            Record(context, wallet, 100m);
            await context.SaveChangesAsync();
            return wallet.Id;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var drop = new NpgsqlCommand($"DROP SCHEMA IF EXISTS {_schema} CASCADE", _connection);
                await drop.ExecuteNonQueryAsync();
            }
            finally
            {
                await _connection.DisposeAsync();
            }
        }
    }
}


