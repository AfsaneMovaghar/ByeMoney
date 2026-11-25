using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ByeMoney.Infrastructure.Persistence;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261124194500_DisableWelcomeCampaignSeed")]
    public partial class DisableWelcomeCampaignSeed : Migration
    {
        private const string WelcomeCampaignId = "88888888-8888-8888-8888-888888888888";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""
                UPDATE "GiftCampaignSettings"
                SET "IsEnabled" = false
                WHERE "Id" = '{WelcomeCampaignId}';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""
                UPDATE "GiftCampaignSettings"
                SET "IsEnabled" = true
                WHERE "Id" = '{WelcomeCampaignId}';
                """);
        }
    }
}