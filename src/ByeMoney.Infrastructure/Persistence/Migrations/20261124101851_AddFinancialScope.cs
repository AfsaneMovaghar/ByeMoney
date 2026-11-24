using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Wallets_UserId",
                table: "Wallets");

            migrationBuilder.DropIndex(
                name: "IX_GiftCampaignSettings_CampaignType",
                table: "GiftCampaignSettings");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_UserId",
                table: "Accounts");

            migrationBuilder.AddColumn<string>(
                name: "ScopeId",
                table: "Wallets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "TARHE_ELAHI");

            migrationBuilder.AddColumn<string>(
                name: "ScopeId",
                table: "TopUpRequests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "TARHE_ELAHI");

            migrationBuilder.AddColumn<string>(
                name: "ScopeId",
                table: "PhysicalProductPurchases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "TARHE_ELAHI");

            migrationBuilder.AddColumn<string>(
                name: "SellerId",
                table: "PhysicalProductPurchases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "TARHE_ELAHI");

            migrationBuilder.AddColumn<string>(
                name: "ScopeId",
                table: "OrderSettlementPeriods",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "TARHE_ELAHI");

            migrationBuilder.AddColumn<string>(
                name: "ScopeId",
                table: "NoorWithdrawals",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "TARHE_ELAHI");

            migrationBuilder.AddColumn<string>(
                name: "ScopeId",
                table: "GiftGrantBatches",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "TARHE_ELAHI");

            migrationBuilder.AddColumn<string>(
                name: "ScopeId",
                table: "GiftCampaignSettings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "TARHE_ELAHI");

            migrationBuilder.AddColumn<string>(
                name: "ScopeId",
                table: "CoursePurchases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "TARHE_ELAHI");

            migrationBuilder.AddColumn<string>(
                name: "SellerId",
                table: "CoursePurchases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "TARHE_ELAHI");

            migrationBuilder.AddColumn<string>(
                name: "ScopeId",
                table: "Carts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "TARHE_ELAHI");

            migrationBuilder.AddColumn<string>(
                name: "ScopeId",
                table: "Accounts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "TARHE_ELAHI");

            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "ScopeId",
                value: "TARHE_ELAHI");

            migrationBuilder.UpdateData(
                table: "GiftCampaignSettings",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                column: "ScopeId",
                value: "TARHE_ELAHI");

            migrationBuilder.Sql("""
                ALTER TABLE "Accounts" ALTER COLUMN "ScopeId" DROP DEFAULT;
                ALTER TABLE "Wallets" ALTER COLUMN "ScopeId" DROP DEFAULT;
                ALTER TABLE "Carts" ALTER COLUMN "ScopeId" DROP DEFAULT;
                ALTER TABLE "CoursePurchases" ALTER COLUMN "ScopeId" DROP DEFAULT;
                ALTER TABLE "CoursePurchases" ALTER COLUMN "SellerId" DROP DEFAULT;
                ALTER TABLE "PhysicalProductPurchases" ALTER COLUMN "ScopeId" DROP DEFAULT;
                ALTER TABLE "PhysicalProductPurchases" ALTER COLUMN "SellerId" DROP DEFAULT;
                ALTER TABLE "TopUpRequests" ALTER COLUMN "ScopeId" DROP DEFAULT;
                ALTER TABLE "GiftCampaignSettings" ALTER COLUMN "ScopeId" DROP DEFAULT;
                ALTER TABLE "GiftGrantBatches" ALTER COLUMN "ScopeId" DROP DEFAULT;
                ALTER TABLE "NoorWithdrawals" ALTER COLUMN "ScopeId" DROP DEFAULT;
                ALTER TABLE "OrderSettlementPeriods" ALTER COLUMN "ScopeId" DROP DEFAULT;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_UserId_ScopeId",
                table: "Wallets",
                columns: new[] { "UserId", "ScopeId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Wallets_ScopeId",
                table: "Wallets",
                sql: "\"ScopeId\" IN ('TARHE_ELAHI', 'BUYMONEY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TopUpRequests_ScopeId",
                table: "TopUpRequests",
                sql: "\"ScopeId\" IN ('TARHE_ELAHI', 'BUYMONEY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalProductPurchases_ScopeId",
                table: "PhysicalProductPurchases",
                sql: "\"ScopeId\" IN ('TARHE_ELAHI', 'BUYMONEY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalProductPurchases_SellerId",
                table: "PhysicalProductPurchases",
                sql: "\"SellerId\" IN ('TARHE_ELAHI', 'BUYMONEY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderSettlementPeriods_ScopeId",
                table: "OrderSettlementPeriods",
                sql: "\"ScopeId\" IN ('TARHE_ELAHI', 'BUYMONEY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NoorWithdrawals_ScopeId",
                table: "NoorWithdrawals",
                sql: "\"ScopeId\" IN ('TARHE_ELAHI', 'BUYMONEY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GiftGrantBatches_ScopeId",
                table: "GiftGrantBatches",
                sql: "\"ScopeId\" IN ('TARHE_ELAHI', 'BUYMONEY')");

            migrationBuilder.CreateIndex(
                name: "IX_GiftCampaignSettings_CampaignType_ScopeId",
                table: "GiftCampaignSettings",
                columns: new[] { "CampaignType", "ScopeId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_GiftCampaignSettings_ScopeId",
                table: "GiftCampaignSettings",
                sql: "\"ScopeId\" IN ('TARHE_ELAHI', 'BUYMONEY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CoursePurchases_ScopeId",
                table: "CoursePurchases",
                sql: "\"ScopeId\" IN ('TARHE_ELAHI', 'BUYMONEY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CoursePurchases_SellerId",
                table: "CoursePurchases",
                sql: "\"SellerId\" IN ('TARHE_ELAHI', 'BUYMONEY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Carts_ScopeId",
                table: "Carts",
                sql: "\"ScopeId\" IN ('TARHE_ELAHI', 'BUYMONEY')");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_ScopeId",
                table: "Accounts",
                column: "ScopeId",
                unique: true,
                filter: "\"Type\" = 'System'");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_UserId_ScopeId",
                table: "Accounts",
                columns: new[] { "UserId", "ScopeId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_ScopeId",
                table: "Accounts",
                sql: "\"ScopeId\" IN ('TARHE_ELAHI', 'BUYMONEY')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Wallets_UserId_ScopeId",
                table: "Wallets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Wallets_ScopeId",
                table: "Wallets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TopUpRequests_ScopeId",
                table: "TopUpRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalProductPurchases_ScopeId",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalProductPurchases_SellerId",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderSettlementPeriods_ScopeId",
                table: "OrderSettlementPeriods");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NoorWithdrawals_ScopeId",
                table: "NoorWithdrawals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GiftGrantBatches_ScopeId",
                table: "GiftGrantBatches");

            migrationBuilder.DropIndex(
                name: "IX_GiftCampaignSettings_CampaignType_ScopeId",
                table: "GiftCampaignSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GiftCampaignSettings_ScopeId",
                table: "GiftCampaignSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CoursePurchases_ScopeId",
                table: "CoursePurchases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CoursePurchases_SellerId",
                table: "CoursePurchases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Carts_ScopeId",
                table: "Carts");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_ScopeId",
                table: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_UserId_ScopeId",
                table: "Accounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_ScopeId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "ScopeId",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "ScopeId",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "ScopeId",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "SellerId",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "ScopeId",
                table: "OrderSettlementPeriods");

            migrationBuilder.DropColumn(
                name: "ScopeId",
                table: "NoorWithdrawals");

            migrationBuilder.DropColumn(
                name: "ScopeId",
                table: "GiftGrantBatches");

            migrationBuilder.DropColumn(
                name: "ScopeId",
                table: "GiftCampaignSettings");

            migrationBuilder.DropColumn(
                name: "ScopeId",
                table: "CoursePurchases");

            migrationBuilder.DropColumn(
                name: "SellerId",
                table: "CoursePurchases");

            migrationBuilder.DropColumn(
                name: "ScopeId",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "ScopeId",
                table: "Accounts");

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_UserId",
                table: "Wallets",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GiftCampaignSettings_CampaignType",
                table: "GiftCampaignSettings",
                column: "CampaignType",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_UserId",
                table: "Accounts",
                column: "UserId",
                unique: true);
        }
    }
}
