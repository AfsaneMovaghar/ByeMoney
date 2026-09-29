using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGatewayTopUpConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankReferenceNumber",
                table: "TopUpRequests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayName",
                table: "TopUpRequests",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.DropIndex(
                name: "IX_TopUpRequests_ExternalTransactionId",
                table: "TopUpRequests");

            migrationBuilder.CreateIndex(
                name: "IX_TopUpRequests_ExternalTransactionId",
                table: "TopUpRequests",
                column: "ExternalTransactionId",
                unique: true,
                filter: "\"PaymentMethod\" = 'Gateway' AND \"ExternalTransactionId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TopUpRequests_ExternalTransactionId",
                table: "TopUpRequests");

            migrationBuilder.CreateIndex(
                name: "IX_TopUpRequests_ExternalTransactionId",
                table: "TopUpRequests",
                column: "ExternalTransactionId");

            migrationBuilder.DropColumn(
                name: "BankReferenceNumber",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "GatewayName",
                table: "TopUpRequests");
        }
    }
}

