using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Gifts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using ByeMoney.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace ByeMoney.UnitTests;

public sealed class FinancialSchemaModelTests
{
    private static readonly IModel Model = BuildModel();

    [Theory]
    [InlineData(typeof(LedgerEntry), typeof(Account), "AccountId")]
    [InlineData(typeof(Wallet), typeof(Account), "AccountId")]
    [InlineData(typeof(Wallet), typeof(User), "UserId")]
    [InlineData(typeof(TopUpRequest), typeof(User), "UserId")]
    [InlineData(typeof(Cart), typeof(User), "BuyerId")]
    [InlineData(typeof(CoursePurchase), typeof(User), "BuyerId")]
    [InlineData(typeof(CoursePurchase), typeof(Cart), "CartId")]
    [InlineData(typeof(PhysicalProductPurchase), typeof(User), "BuyerId")]
    [InlineData(typeof(PhysicalProductPurchase), typeof(Cart), "CartId")]
    [InlineData(typeof(GiftGrantRecipient), typeof(User), "UserId")]
    public void FinancialTables_HaveRestrictForeignKey(Type dependent, Type principal, string property)
    {
        var foreignKey = Model.FindEntityType(dependent)!
            .GetForeignKeys()
            .SingleOrDefault(key =>
                key.PrincipalEntityType.ClrType == principal &&
                key.Properties.Single().Name == property);

        foreignKey.Should().NotBeNull();
        foreignKey!.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
    }

    [Fact]
    public void LedgerEntries_HaveReferenceIndexAndNoSingleColumnEnumIndexes()
    {
        var indexes = Model.FindEntityType(typeof(LedgerEntry))!
            .GetIndexes()
            .Select(index => string.Join(",", index.Properties.Select(property => property.Name)))
            .ToList();

        indexes.Should().Contain("ReferenceType,ReferenceId");
        indexes.Should().NotContain("ReferenceType");
        indexes.Should().NotContain("BalanceType");
    }

    [Theory]
    [InlineData(typeof(LedgerEntry), "ReferenceType")]
    [InlineData(typeof(LedgerEntry), "BalanceType")]
    [InlineData(typeof(CoursePurchase), "Status")]
    [InlineData(typeof(GiftCampaignSetting), "CampaignType")]
    [InlineData(typeof(GiftGrantBatch), "Source")]
    public void EnumColumns_AreStoredAsStrings(Type entity, string property)
    {
        Model.FindEntityType(entity)!
            .FindProperty(property)!
            .GetProviderClrType()
            .Should().Be(typeof(string));
    }

    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only")
            .Options;

        using var context = new ApplicationDbContext(options);

        return context.Model;
    }
}
