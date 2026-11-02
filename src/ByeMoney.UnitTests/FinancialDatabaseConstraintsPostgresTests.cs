using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ByeMoney.UnitTests;

public sealed class FinancialDatabaseConstraintsPostgresTests : IAsyncLifetime
{
    private const string CheckViolationSqlState = PostgresErrorCodes.CheckViolation;

    private readonly string _schema = "fin_constraints_test_" + Guid.NewGuid().ToString("N");
    private string? _adminConnection;
    private string _schemaConnection = null!;

    public async Task InitializeAsync()
    {
        _adminConnection = Environment.GetEnvironmentVariable("BYEMONEY_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(_adminConnection))
        {
            _adminConnection = null;
            return;
        }

        await ExecuteAsync(_adminConnection, $"CREATE SCHEMA \"{_schema}\"");

        _schemaConnection = new NpgsqlConnectionStringBuilder(_adminConnection)
        {
            SearchPath = _schema,
            Pooling = false
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_schemaConnection, postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", _schema))
            .Options;

        await using var context = new ApplicationDbContext(options);
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_adminConnection is null)
        {
            return;
        }

        await ExecuteAsync(_adminConnection, $"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE");
    }

    [PostgresFact]
    public async Task MigratedEmptyDatabase_ContainsSystemAccount()
    {
        await using var connection = new NpgsqlConnection(_schemaConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM \"Accounts\" WHERE \"Id\" = @id AND \"Type\" = 'System' AND \"UserId\" IS NULL",
            connection);
        command.Parameters.AddWithValue("id", Account.SystemAccountId.Value);

        var count = (long)(await command.ExecuteScalarAsync())!;

        count.Should().Be(1);
    }

    [PostgresTheory]
    [InlineData("-1")]
    public async Task Wallets_RejectNegativeBalances(string value)
    {
        foreach (var column in new[] { "Balance", "GiftBalance" })
        {
            var regular = column == "Balance" ? value : "0";
            var gift = column == "GiftBalance" ? value : "0";

            await AssertCheckViolationAsync(
                "INSERT INTO \"Wallets\" (\"Id\", \"AccountId\", \"UserId\", \"Balance\", \"GiftBalance\", \"LastUpdatedAtUtc\", \"CreatedAtUtc\") " +
                $"VALUES (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), {regular}, {gift}, now(), now())");
        }
    }

    [PostgresTheory]
    [InlineData("0")]
    public async Task LedgerEntries_RejectZeroAmount(string value)
    {
        await AssertCheckViolationAsync(
            "INSERT INTO \"LedgerEntries\" (\"Id\", \"AccountId\", \"Amount\", \"TransactionId\", \"ReferenceType\", \"BalanceType\", \"CreatedAtUtc\") " +
            $"VALUES (gen_random_uuid(), gen_random_uuid(), {value}, gen_random_uuid(), 'Purchase', 'Regular', now())");
    }

    [PostgresFact]
    public async Task LedgerEntries_RejectUnknownEnumValues()
    {
        await AssertCheckViolationAsync(LedgerInsertSql("'Unknown'", "'Regular'"));
        await AssertCheckViolationAsync(LedgerInsertSql("'Purchase'", "'Unknown'"));
    }

    [PostgresFact]
    public async Task LedgerEntries_RejectOrphanAccount()
    {
        await using var connection = new NpgsqlConnection(_schemaConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(LedgerInsertSql("'Purchase'", "'Regular'"), connection);

        var act = async () => await command.ExecuteNonQueryAsync();

        var exception = await act.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
    }

    [PostgresFact]
    public async Task Wallets_RejectOrphanAccountAndUser()
    {
        await using var connection = new NpgsqlConnection(_schemaConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "INSERT INTO \"Wallets\" (\"Id\", \"AccountId\", \"UserId\", \"Balance\", \"GiftBalance\", \"LastUpdatedAtUtc\", \"CreatedAtUtc\") " +
            "VALUES (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), 0, 0, now(), now())",
            connection);

        var act = async () => await command.ExecuteNonQueryAsync();

        var exception = await act.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
    }

    private static string LedgerInsertSql(string referenceType, string balanceType) =>
        "INSERT INTO \"LedgerEntries\" (\"Id\", \"AccountId\", \"Amount\", \"TransactionId\", \"ReferenceType\", \"BalanceType\", \"CreatedAtUtc\") " +
        $"VALUES (gen_random_uuid(), gen_random_uuid(), 10, gen_random_uuid(), {referenceType}, {balanceType}, now())";

    [PostgresTheory]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task GiftGrantBatches_RejectNonPositiveAmounts(string value)
    {
        await AssertCheckViolationAsync(
            "INSERT INTO \"GiftGrantBatches\" (\"Id\", \"IdempotencyKey\", \"RequestPayloadHash\", \"Source\", \"Reason\", " +
            "\"AmountNoorPerUser\", \"TotalRecipients\", \"TotalAmountNoor\", \"CreatedAtUtc\") " +
            $"VALUES (gen_random_uuid(), 'k', 'h', 'AdminBatch', 'r', {value}, 1, 10, now())");
    }

    private async Task AssertCheckViolationAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_schemaConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);

        var act = async () => await command.ExecuteNonQueryAsync();

        var exception = await act.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(CheckViolationSqlState);
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
