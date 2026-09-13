using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCoursePurchaseAndSettingsAndLedgerTransactionType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TransactionType",
                table: "LedgerEntries",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "CoursePurchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProductSource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExternalProductId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProductTitle = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PriceInRialAtPurchaseTime = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ConversionRateAtPurchaseTime = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PriceInNoorAtPurchaseTime = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    NotificationAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastNotificationAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotificationFailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoursePurchases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Key);
                });

            migrationBuilder.InsertData(
                table: "SystemSettings",
                columns: new[] { "Key", "Description", "UpdatedAtUtc", "Value" },
                values: new object[] { "ConversionRate:RialToNoor", "Rial to Noor conversion rate (Rials per 1 Noor).", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "1000" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_TransactionType",
                table: "LedgerEntries",
                column: "TransactionType");

            migrationBuilder.CreateIndex(
                name: "IX_CoursePurchases_Status",
                table: "CoursePurchases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CoursePurchases_UserId_ExternalProductId",
                table: "CoursePurchases",
                columns: new[] { "UserId", "ExternalProductId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoursePurchases");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_TransactionType",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "TransactionType",
                table: "LedgerEntries");
        }
    }
}
