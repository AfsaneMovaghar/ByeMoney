using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCarrierAlertAndTrackingUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CarrierAlertAtUtc",
                table: "PhysicalProductPurchases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CarrierAlertCode",
                table: "PhysicalProductPurchases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CarrierAlertResolution",
                table: "PhysicalProductPurchases",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CarrierAlertResolutionNote",
                table: "PhysicalProductPurchases",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CarrierAlertResolvedAtUtc",
                table: "PhysicalProductPurchases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CarrierAlertResolvedByUserId",
                table: "PhysicalProductPurchases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrackingUrl",
                table: "PhysicalProductPurchases",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalProductPurchases_CarrierAlertCode",
                table: "PhysicalProductPurchases",
                sql: "\"CarrierAlertCode\" IS NULL OR \"CarrierAlertCode\" IN (11304, 11305)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalProductPurchases_CarrierAlertResolution",
                table: "PhysicalProductPurchases",
                sql: "\"CarrierAlertResolution\" IS NULL OR (\"CarrierAlertCode\" IS NOT NULL AND \"CarrierAlertResolution\" IN ('DeliveryConfirmed', 'ClosedWithoutSettlement'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalProductPurchases_CarrierAlertCode",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalProductPurchases_CarrierAlertResolution",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "CarrierAlertAtUtc",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "CarrierAlertCode",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "CarrierAlertResolution",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "CarrierAlertResolutionNote",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "CarrierAlertResolvedAtUtc",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "CarrierAlertResolvedByUserId",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "TrackingUrl",
                table: "PhysicalProductPurchases");
        }
    }
}
