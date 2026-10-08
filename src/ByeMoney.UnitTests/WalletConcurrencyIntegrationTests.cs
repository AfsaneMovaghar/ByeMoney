using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using ByeMoney.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ByeMoney.UnitTests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BYEMONEY_TEST_CONNECTION_STRING")))
            Skip = "برای آزمون دیتابیس، اتصال محلی BYEMONEY_TEST_CONNECTION_STRING لازم است.";
    }
}

public class WalletConcurrencyIntegrationTests
{
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
        (await check.Set<Wallet>().SingleAsync()).Balance.Should().Be(130m);
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

        await FluentActions.Awaiting(() => second.SaveChangesAsync()).Should().ThrowAsync<DomainException>();

        await using var check = database.Context();
        (await check.Set<Wallet>().SingleAsync()).Balance.Should().Be(20m);
        var entries = await check.Set<LedgerEntry>().ToListAsync();
        entries.Should().HaveCount(4);
        entries.Where(x => x.AccountId == wallet1.AccountId).Sum(x => x.Amount).Should().Be(20m);
        entries.Sum(x => x.Amount).Should().Be(0m);
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
