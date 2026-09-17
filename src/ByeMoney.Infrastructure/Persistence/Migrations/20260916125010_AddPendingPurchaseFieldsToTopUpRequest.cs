using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingPurchaseFieldsToTopUpRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PendingItemExternalId",
                table: "TopUpRequests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingItemType",
                table: "TopUpRequests",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingPriceSnapshot",
                table: "TopUpRequests",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingRateSnapshot",
                table: "TopUpRequests",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingItemExternalId",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "PendingItemType",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "PendingPriceSnapshot",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "PendingRateSnapshot",
                table: "TopUpRequests");
        }
    }
}
