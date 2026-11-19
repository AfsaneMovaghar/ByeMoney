using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCarrierParcelState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // سه ستون nullable؛ ردیف‌های موجود بدون تغییر مقدار عبور می‌کنند.
            migrationBuilder.AddColumn<int>(
                name: "CarrierParcelQuantity",
                table: "PhysicalProductPurchases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CarrierPaymentPendingSinceUtc",
                table: "PhysicalProductPurchases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShipmentRequestStartedAtUtc",
                table: "PhysicalProductPurchases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalProductPurchases_CarrierParcelQuantity",
                table: "PhysicalProductPurchases",
                sql: "\"CarrierParcelQuantity\" IS NULL OR \"CarrierParcelQuantity\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalProductPurchases_CarrierPaymentPending",
                table: "PhysicalProductPurchases",
                sql: "\"CarrierPaymentPendingSinceUtc\" IS NULL OR (\"ProviderParcelId\" IS NOT NULL AND \"DeliveryStatus\" = 'Preparing')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalProductPurchases_CarrierPaymentPending",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalProductPurchases_CarrierParcelQuantity",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "CarrierParcelQuantity",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "CarrierPaymentPendingSinceUtc",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "ShipmentRequestStartedAtUtc",
                table: "PhysicalProductPurchases");
        }
    }
}
