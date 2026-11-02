using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialAmountChecksAndSystemAccountSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Accounts",
                columns: new[] { "Id", "CreatedAtUtc", "Status", "Type", "UpdatedAt", "UserId" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), "Active", "System", null, null });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Wallets_Balance_NonNegative",
                table: "Wallets",
                sql: "\"Balance\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Wallets_GiftBalance_NonNegative",
                table: "Wallets",
                sql: "\"GiftBalance\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TopUpRequests_AmountNoor_Positive",
                table: "TopUpRequests",
                sql: "\"AmountNoor\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LedgerEntries_Amount_NonZero",
                table: "LedgerEntries",
                sql: "\"Amount\" <> 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GiftGrantRecipients_AmountNoor_Positive",
                table: "GiftGrantRecipients",
                sql: "\"AmountNoor\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GiftGrantRecipients_NewGiftBalance_NonNegative",
                table: "GiftGrantRecipients",
                sql: "\"NewGiftBalance\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GiftGrantBatches_AmountNoorPerUser_Positive",
                table: "GiftGrantBatches",
                sql: "\"AmountNoorPerUser\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GiftGrantBatches_TotalAmountNoor_Positive",
                table: "GiftGrantBatches",
                sql: "\"TotalAmountNoor\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Wallets_Balance_NonNegative",
                table: "Wallets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Wallets_GiftBalance_NonNegative",
                table: "Wallets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TopUpRequests_AmountNoor_Positive",
                table: "TopUpRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LedgerEntries_Amount_NonZero",
                table: "LedgerEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GiftGrantRecipients_AmountNoor_Positive",
                table: "GiftGrantRecipients");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GiftGrantRecipients_NewGiftBalance_NonNegative",
                table: "GiftGrantRecipients");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GiftGrantBatches_AmountNoorPerUser_Positive",
                table: "GiftGrantBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GiftGrantBatches_TotalAmountNoor_Positive",
                table: "GiftGrantBatches");

            migrationBuilder.DeleteData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"));
        }
    }
}
