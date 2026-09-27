using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminAssistedCardToCardTopUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmountRial",
                table: "TopUpRequests",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChargeType",
                table: "TopUpRequests",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "TopUpRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "TopUpRequests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptId",
                table: "TopUpRequests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RialPerNoorSnapshot",
                table: "TopUpRequests",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PerformedByUserId",
                table: "LedgerEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopUpRequests_IdempotencyKey",
                table: "TopUpRequests",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TopUpRequests_IdempotencyKey",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "AmountRial",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "ChargeType",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "ReceiptId",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "RialPerNoorSnapshot",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "PerformedByUserId",
                table: "LedgerEntries");
        }
    }
}
