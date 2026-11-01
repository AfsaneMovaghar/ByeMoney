using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhysicalDeliveryStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAtUtc",
                table: "PhysicalProductPurchases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryStatus",
                table: "PhysicalProductPurchases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Preparing");

            migrationBuilder.AddColumn<DateTime>(
                name: "ShippedAtUtc",
                table: "PhysicalProductPurchases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalProductPurchases_DeliveryStatus",
                table: "PhysicalProductPurchases",
                sql: "\"DeliveryStatus\" IN ('Preparing', 'Shipped', 'Delivered')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalProductPurchases_DeliveryStatus",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "DeliveredAtUtc",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "DeliveryStatus",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "ShippedAtUtc",
                table: "PhysicalProductPurchases");
        }
    }
}
