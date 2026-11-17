using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentParcelFactors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ParcelDeclaredValueIrr",
                table: "PhysicalProductPurchases",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParcelHeightCm",
                table: "PhysicalProductPurchases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ParcelIsFragile",
                table: "PhysicalProductPurchases",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParcelLengthCm",
                table: "PhysicalProductPurchases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParcelWeightGrams",
                table: "PhysicalProductPurchases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParcelWidthCm",
                table: "PhysicalProductPurchases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ParcelDeclaredValueIrr",
                table: "CartItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParcelHeightCm",
                table: "CartItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ParcelIsFragile",
                table: "CartItems",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParcelLengthCm",
                table: "CartItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParcelWeightGrams",
                table: "CartItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParcelWidthCm",
                table: "CartItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalProductPurchases_ParcelFacts",
                table: "PhysicalProductPurchases",
                sql: "(\"ParcelWeightGrams\" IS NULL AND \"ParcelLengthCm\" IS NULL AND \"ParcelWidthCm\" IS NULL AND \"ParcelHeightCm\" IS NULL AND \"ParcelDeclaredValueIrr\" IS NULL AND \"ParcelIsFragile\" IS NULL) OR (\"ParcelWeightGrams\" > 0 AND \"ParcelLengthCm\" > 0 AND \"ParcelWidthCm\" > 0 AND \"ParcelHeightCm\" > 0 AND \"ParcelDeclaredValueIrr\" > 0 AND \"ParcelIsFragile\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CartItems_ParcelFacts",
                table: "CartItems",
                sql: "(\"ParcelWeightGrams\" IS NULL AND \"ParcelLengthCm\" IS NULL AND \"ParcelWidthCm\" IS NULL AND \"ParcelHeightCm\" IS NULL AND \"ParcelDeclaredValueIrr\" IS NULL AND \"ParcelIsFragile\" IS NULL) OR (\"ParcelWeightGrams\" > 0 AND \"ParcelLengthCm\" > 0 AND \"ParcelWidthCm\" > 0 AND \"ParcelHeightCm\" > 0 AND \"ParcelDeclaredValueIrr\" > 0 AND \"ParcelIsFragile\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalProductPurchases_ParcelFacts",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CartItems_ParcelFacts",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "ParcelDeclaredValueIrr",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "ParcelHeightCm",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "ParcelIsFragile",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "ParcelLengthCm",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "ParcelWeightGrams",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "ParcelWidthCm",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "ParcelDeclaredValueIrr",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "ParcelHeightCm",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "ParcelIsFragile",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "ParcelLengthCm",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "ParcelWeightGrams",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "ParcelWidthCm",
                table: "CartItems");
        }
    }
}
