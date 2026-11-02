using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenFinancialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_BalanceType",
                table: "LedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_ReferenceType",
                table: "LedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_CoursePurchases_UserId_ExternalProductId",
                table: "CoursePurchases");

            // ایندکس خام قدیمی بدون ItemType؛ تا حذف نشود خرید فصل و دورهٔ هم‌شناسه رد می‌شود.
            migrationBuilder.DropIndex(
                name: "IX_CoursePurchases_BuyerCourse",
                table: "CoursePurchases");

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceType",
                table: "LedgerEntries",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "BalanceType",
                table: "LedgerEntries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Regular",
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<string>(
                name: "Source",
                table: "GiftGrantBatches",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "CampaignType",
                table: "GiftCampaignSettings",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "CoursePurchases",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            ConvertNumbersToNames(migrationBuilder, "LedgerEntries", "ReferenceType", "TopUp", "Purchase", "Refund", "AdminGrant", "WelcomeGift");
            ConvertNumbersToNames(migrationBuilder, "LedgerEntries", "BalanceType", "Regular", "Gift");
            ConvertNumbersToNames(migrationBuilder, "GiftGrantBatches", "Source", "AdminBatch", "WelcomeBonus");
            ConvertNumbersToNames(migrationBuilder, "GiftCampaignSettings", "CampaignType", "Welcome");
            ConvertNumbersToNames(migrationBuilder, "CoursePurchases", "Status", "Pending", "Debited", "NotificationSent", "NotificationFailed");

            migrationBuilder.UpdateData(
                table: "GiftCampaignSettings",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                column: "CampaignType",
                value: "Welcome");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_ReferenceType_ReferenceId",
                table: "LedgerEntries",
                columns: new[] { "ReferenceType", "ReferenceId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_LedgerEntries_BalanceType",
                table: "LedgerEntries",
                sql: "\"BalanceType\" IN ('Regular', 'Gift')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LedgerEntries_ReferenceType",
                table: "LedgerEntries",
                sql: "\"ReferenceType\" IN ('TopUp', 'Purchase', 'Refund', 'AdminGrant', 'WelcomeGift')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GiftGrantBatches_Source",
                table: "GiftGrantBatches",
                sql: "\"Source\" IN ('AdminBatch', 'WelcomeBonus')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GiftCampaignSettings_CampaignType",
                table: "GiftCampaignSettings",
                sql: "\"CampaignType\" IN ('Welcome')");

            migrationBuilder.CreateIndex(
                name: "IX_CoursePurchases_UserId_ExternalProductId",
                table: "CoursePurchases",
                columns: new[] { "UserId", "ExternalProductId", "ItemType" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CoursePurchases_Status",
                table: "CoursePurchases",
                sql: "\"Status\" IN ('Pending', 'Debited', 'NotificationSent', 'NotificationFailed')");

            // مهاجرت پاک‌سازی سبدها (۲۰۲۶-۱۰-۳۱) سبدها را حذف و خریدها را نگه داشته است؛ پیوند به سبدِ ناموجود قطع می‌شود تا FK بنشیند.
            migrationBuilder.Sql(
                "UPDATE \"CoursePurchases\" SET \"CartId\" = NULL " +
                "WHERE \"CartId\" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM \"Carts\" WHERE \"Carts\".\"Id\" = \"CoursePurchases\".\"CartId\")");

            migrationBuilder.AddForeignKey(
                name: "FK_Carts_Users_BuyerId",
                table: "Carts",
                column: "BuyerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CoursePurchases_Carts_CartId",
                table: "CoursePurchases",
                column: "CartId",
                principalTable: "Carts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CoursePurchases_Users_UserId",
                table: "CoursePurchases",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GiftGrantRecipients_Users_UserId",
                table: "GiftGrantRecipients",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LedgerEntries_Accounts_AccountId",
                table: "LedgerEntries",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PhysicalProductPurchases_Carts_CartId",
                table: "PhysicalProductPurchases",
                column: "CartId",
                principalTable: "Carts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PhysicalProductPurchases_Users_UserId",
                table: "PhysicalProductPurchases",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TopUpRequests_Users_UserId",
                table: "TopUpRequests",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Carts_Users_BuyerId",
                table: "Carts");

            migrationBuilder.DropForeignKey(
                name: "FK_CoursePurchases_Carts_CartId",
                table: "CoursePurchases");

            migrationBuilder.DropForeignKey(
                name: "FK_CoursePurchases_Users_UserId",
                table: "CoursePurchases");

            migrationBuilder.DropForeignKey(
                name: "FK_GiftGrantRecipients_Users_UserId",
                table: "GiftGrantRecipients");

            migrationBuilder.DropForeignKey(
                name: "FK_LedgerEntries_Accounts_AccountId",
                table: "LedgerEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysicalProductPurchases_Carts_CartId",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysicalProductPurchases_Users_UserId",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropForeignKey(
                name: "FK_TopUpRequests_Users_UserId",
                table: "TopUpRequests");

            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_ReferenceType_ReferenceId",
                table: "LedgerEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LedgerEntries_BalanceType",
                table: "LedgerEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LedgerEntries_ReferenceType",
                table: "LedgerEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GiftGrantBatches_Source",
                table: "GiftGrantBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GiftCampaignSettings_CampaignType",
                table: "GiftCampaignSettings");

            migrationBuilder.DropIndex(
                name: "IX_CoursePurchases_UserId_ExternalProductId",
                table: "CoursePurchases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CoursePurchases_Status",
                table: "CoursePurchases");

            migrationBuilder.Sql("ALTER TABLE \"LedgerEntries\" ALTER COLUMN \"BalanceType\" DROP DEFAULT");
            ConvertNamesToNumbers(migrationBuilder, "LedgerEntries", "ReferenceType", "TopUp", "Purchase", "Refund", "AdminGrant", "WelcomeGift");
            ConvertNamesToNumbers(migrationBuilder, "LedgerEntries", "BalanceType", "Regular", "Gift");
            ConvertNamesToNumbers(migrationBuilder, "GiftGrantBatches", "Source", "AdminBatch", "WelcomeBonus");
            ConvertNamesToNumbers(migrationBuilder, "GiftCampaignSettings", "CampaignType", "Welcome");
            ConvertNamesToNumbers(migrationBuilder, "CoursePurchases", "Status", "Pending", "Debited", "NotificationSent", "NotificationFailed");

            migrationBuilder.AlterColumn<int>(
                name: "ReferenceType",
                table: "LedgerEntries",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<int>(
                name: "BalanceType",
                table: "LedgerEntries",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Regular");

            migrationBuilder.AlterColumn<int>(
                name: "Source",
                table: "GiftGrantBatches",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<int>(
                name: "CampaignType",
                table: "GiftCampaignSettings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "CoursePurchases",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.UpdateData(
                table: "GiftCampaignSettings",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"),
                column: "CampaignType",
                value: 1);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_BalanceType",
                table: "LedgerEntries",
                column: "BalanceType");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_ReferenceType",
                table: "LedgerEntries",
                column: "ReferenceType");

            migrationBuilder.CreateIndex(
                name: "IX_CoursePurchases_UserId_ExternalProductId",
                table: "CoursePurchases",
                columns: new[] { "UserId", "ExternalProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoursePurchases_BuyerCourse",
                table: "CoursePurchases",
                columns: new[] { "UserId", "ExternalProductId" },
                unique: true);
        }

        // مقدارهای Enum از عدد (ترتیب ۱ به بعد) به نام تبدیل می‌شوند تا ردیف‌های موجود قید IN را نقض نکنند.
        // نام‌ها ثابت‌های migration هستند، نه ورودی کاربر.
        private static void ConvertNumbersToNames(
            MigrationBuilder migrationBuilder,
            string table,
            string column,
            params string[] names)
        {
            var cases = string.Join(" ", names.Select((name, index) => $"WHEN '{index + 1}' THEN '{name}'"));
            migrationBuilder.Sql($"UPDATE \"{table}\" SET \"{column}\" = CASE \"{column}\" {cases} END");
        }

        // ستون رشته‌ای را با نگاشت نام به عدد به integer برمی‌گرداند؛ PostgreSQL خودکار نمی‌تواند.
        private static void ConvertNamesToNumbers(
            MigrationBuilder migrationBuilder,
            string table,
            string column,
            params string[] names)
        {
            var cases = string.Join(" ", names.Select((name, index) => $"WHEN '{name}' THEN {index + 1}"));
            migrationBuilder.Sql(
                $"ALTER TABLE \"{table}\" ALTER COLUMN \"{column}\" TYPE integer USING CASE \"{column}\" {cases} END");
        }
    }
}
