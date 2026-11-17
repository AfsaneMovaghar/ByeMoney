using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCarrierReturnResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CarrierReturnResolution",
                table: "PhysicalProductPurchases",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CarrierReturnResolutionNote",
                table: "PhysicalProductPurchases",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CarrierReturnResolvedAtUtc",
                table: "PhysicalProductPurchases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CarrierReturnResolvedByUserId",
                table: "PhysicalProductPurchases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalProductPurchases_CarrierReturnResolution",
                table: "PhysicalProductPurchases",
                sql: "\"CarrierReturnResolution\" IS NULL OR \"CarrierReturnResolution\" IN ('DeliveryConfirmed', 'ClosedWithoutSettlement')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalProductPurchases_CarrierReturnResolution",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "CarrierReturnResolution",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "CarrierReturnResolutionNote",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "CarrierReturnResolvedAtUtc",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "CarrierReturnResolvedByUserId",
                table: "PhysicalProductPurchases");
        }
    }
}
