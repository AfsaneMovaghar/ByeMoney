using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhysicalProductPurchaseReferenceRateSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ReferenceRialPerNoorAtPurchaseTime",
                table: "PhysicalProductPurchases",
                type: "numeric(18,0)",
                precision: 18,
                scale: 0,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReferenceRialPerNoorAtPurchaseTime",
                table: "PhysicalProductPurchases");
        }
    }
}
