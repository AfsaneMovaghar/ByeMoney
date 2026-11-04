using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhysicalCancellationStockReturn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StockReturnErrorCode",
                table: "PhysicalProductCancellations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StockReturnedAtUtc",
                table: "PhysicalProductCancellations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalProductCancellations_PendingStockReturn",
                table: "PhysicalProductCancellations",
                column: "OperationId",
                filter: "\"StockReturnedAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PhysicalProductCancellations_PendingStockReturn",
                table: "PhysicalProductCancellations");

            migrationBuilder.DropColumn(
                name: "StockReturnErrorCode",
                table: "PhysicalProductCancellations");

            migrationBuilder.DropColumn(
                name: "StockReturnedAtUtc",
                table: "PhysicalProductCancellations");
        }
    }
}
