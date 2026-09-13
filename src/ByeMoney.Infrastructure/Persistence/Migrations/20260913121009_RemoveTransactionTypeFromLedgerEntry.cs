using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTransactionTypeFromLedgerEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_TransactionType",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "TransactionType",
                table: "LedgerEntries");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_ReferenceType",
                table: "LedgerEntries",
                column: "ReferenceType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_ReferenceType",
                table: "LedgerEntries");

            migrationBuilder.AddColumn<int>(
                name: "TransactionType",
                table: "LedgerEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_TransactionType",
                table: "LedgerEntries",
                column: "TransactionType");
        }
    }
}
