using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhysicalShipmentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ProviderParcelId",
                table: "PhysicalProductPurchases",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrackingCode",
                table: "PhysicalProductPurchases",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrackingProvider",
                table: "PhysicalProductPurchases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalProductPurchases_DeliveryStatus_ShippedAtUtc",
                table: "PhysicalProductPurchases",
                columns: new[] { "DeliveryStatus", "ShippedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalProductPurchases_TrackingCode",
                table: "PhysicalProductPurchases",
                column: "TrackingCode");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalProductPurchases_TrackingProvider",
                table: "PhysicalProductPurchases",
                sql: "\"TrackingProvider\" IS NULL OR \"TrackingProvider\" IN ('PostEx', 'Manual')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PhysicalProductPurchases_DeliveryStatus_ShippedAtUtc",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalProductPurchases_TrackingCode",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalProductPurchases_TrackingProvider",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "ProviderParcelId",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "TrackingCode",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "TrackingProvider",
                table: "PhysicalProductPurchases");
        }
    }
}
