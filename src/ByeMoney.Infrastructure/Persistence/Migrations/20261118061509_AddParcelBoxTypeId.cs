using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddParcelBoxTypeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalProductPurchases_ParcelFacts",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CartItems_ParcelFacts",
                table: "CartItems");

            migrationBuilder.AddColumn<int>(
                name: "ParcelBoxTypeId",
                table: "PhysicalProductPurchases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParcelBoxTypeId",
                table: "CartItems",
                type: "integer",
                nullable: true);

            // عوامل قبلی بدون شناسهٔ جعبه ناقص‌اند؛ دادهٔ توسعه پاک می‌شود تا قید همه‌یا‌هیچ برقرار شود.
            migrationBuilder.Sql(
                """
                UPDATE "CartItems" SET
                    "ParcelWeightGrams" = NULL,
                    "ParcelLengthCm" = NULL,
                    "ParcelWidthCm" = NULL,
                    "ParcelHeightCm" = NULL,
                    "ParcelDeclaredValueIrr" = NULL,
                    "ParcelIsFragile" = NULL,
                    "ParcelBoxTypeId" = NULL;
                UPDATE "PhysicalProductPurchases" SET
                    "ParcelWeightGrams" = NULL,
                    "ParcelLengthCm" = NULL,
                    "ParcelWidthCm" = NULL,
                    "ParcelHeightCm" = NULL,
                    "ParcelDeclaredValueIrr" = NULL,
                    "ParcelIsFragile" = NULL,
                    "ParcelBoxTypeId" = NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalProductPurchases_ParcelFacts",
                table: "PhysicalProductPurchases",
                sql: "(\"ParcelWeightGrams\" IS NULL AND \"ParcelLengthCm\" IS NULL AND \"ParcelWidthCm\" IS NULL AND \"ParcelHeightCm\" IS NULL AND \"ParcelDeclaredValueIrr\" IS NULL AND \"ParcelIsFragile\" IS NULL AND \"ParcelBoxTypeId\" IS NULL) OR (\"ParcelWeightGrams\" > 0 AND \"ParcelLengthCm\" > 0 AND \"ParcelWidthCm\" > 0 AND \"ParcelHeightCm\" > 0 AND \"ParcelDeclaredValueIrr\" > 0 AND \"ParcelIsFragile\" IS NOT NULL AND \"ParcelBoxTypeId\" >= 1 AND \"ParcelBoxTypeId\" <= 12)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CartItems_ParcelFacts",
                table: "CartItems",
                sql: "(\"ParcelWeightGrams\" IS NULL AND \"ParcelLengthCm\" IS NULL AND \"ParcelWidthCm\" IS NULL AND \"ParcelHeightCm\" IS NULL AND \"ParcelDeclaredValueIrr\" IS NULL AND \"ParcelIsFragile\" IS NULL AND \"ParcelBoxTypeId\" IS NULL) OR (\"ParcelWeightGrams\" > 0 AND \"ParcelLengthCm\" > 0 AND \"ParcelWidthCm\" > 0 AND \"ParcelHeightCm\" > 0 AND \"ParcelDeclaredValueIrr\" > 0 AND \"ParcelIsFragile\" IS NOT NULL AND \"ParcelBoxTypeId\" >= 1 AND \"ParcelBoxTypeId\" <= 12)");
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
                name: "ParcelBoxTypeId",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "ParcelBoxTypeId",
                table: "CartItems");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalProductPurchases_ParcelFacts",
                table: "PhysicalProductPurchases",
                sql: "(\"ParcelWeightGrams\" IS NULL AND \"ParcelLengthCm\" IS NULL AND \"ParcelWidthCm\" IS NULL AND \"ParcelHeightCm\" IS NULL AND \"ParcelDeclaredValueIrr\" IS NULL AND \"ParcelIsFragile\" IS NULL) OR (\"ParcelWeightGrams\" > 0 AND \"ParcelLengthCm\" > 0 AND \"ParcelWidthCm\" > 0 AND \"ParcelHeightCm\" > 0 AND \"ParcelDeclaredValueIrr\" > 0 AND \"ParcelIsFragile\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CartItems_ParcelFacts",
                table: "CartItems",
                sql: "(\"ParcelWeightGrams\" IS NULL AND \"ParcelLengthCm\" IS NULL AND \"ParcelWidthCm\" IS NULL AND \"ParcelHeightCm\" IS NULL AND \"ParcelDeclaredValueIrr\" IS NULL AND \"ParcelIsFragile\" IS NULL) OR (\"ParcelWeightGrams\" > 0 AND \"ParcelLengthCm\" > 0 AND \"ParcelWidthCm\" > 0 AND \"ParcelHeightCm\" > 0 AND \"ParcelDeclaredValueIrr\" > 0 AND \"ParcelIsFragile\" IS NOT NULL)");
        }
    }
}
