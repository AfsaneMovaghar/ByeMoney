using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameTopUpAmountToAmountNoor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Amount",
                table: "TopUpRequests",
                newName: "AmountNoor");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AmountNoor",
                table: "TopUpRequests",
                newName: "Amount");
        }
    }
}
