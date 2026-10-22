using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhysicalProductCartPurchases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CourseCarts_Discounts",
                table: "CourseCarts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CourseCartPurchaseAllocations",
                table: "CourseCartPurchaseAllocations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CourseCartItems",
                table: "CourseCartItems");

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingAmountInNoor",
                table: "CourseCarts",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ItemType",
                table: "CourseCartPurchaseAllocations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Course");

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "CourseCartItems",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddPrimaryKey(
                name: "PK_CourseCartPurchaseAllocations",
                table: "CourseCartPurchaseAllocations",
                columns: new[] { "CartId", "ExternalCourseId", "ItemType" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_CourseCartItems",
                table: "CourseCartItems",
                columns: new[] { "CartId", "ExternalCourseId", "ItemType" });

            migrationBuilder.CreateTable(
                name: "PhysicalProductPurchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CartId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalProductId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProductTitle = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPriceInNoor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PaidAmountInNoor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalProductPurchases", x => x.Id);
                    table.CheckConstraint("CK_PhysicalProductPurchases_NonNegativeAmounts", "\"UnitPriceInNoor\" >= 0 AND \"PaidAmountInNoor\" >= 0 AND trunc(\"UnitPriceInNoor\") = \"UnitPriceInNoor\" AND trunc(\"PaidAmountInNoor\") = \"PaidAmountInNoor\"");
                    table.CheckConstraint("CK_PhysicalProductPurchases_Quantity", "\"Quantity\" >= 1");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_CourseCarts_Discounts",
                table: "CourseCarts",
                sql: "\"ItemDiscountInNoor\" >= 0 AND \"CouponDiscountInNoor\" >= 0 AND \"ShippingAmountInNoor\" >= 0 AND trunc(\"ItemDiscountInNoor\") = \"ItemDiscountInNoor\" AND trunc(\"CouponDiscountInNoor\") = \"CouponDiscountInNoor\" AND trunc(\"ShippingAmountInNoor\") = \"ShippingAmountInNoor\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CourseCartItems_Quantity",
                table: "CourseCartItems",
                sql: "\"Quantity\" >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalProductPurchases_CartId",
                table: "PhysicalProductPurchases",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalProductPurchases_UserId",
                table: "PhysicalProductPurchases",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhysicalProductPurchases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CourseCarts_Discounts",
                table: "CourseCarts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CourseCartPurchaseAllocations",
                table: "CourseCartPurchaseAllocations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CourseCartItems",
                table: "CourseCartItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CourseCartItems_Quantity",
                table: "CourseCartItems");

            migrationBuilder.DropColumn(
                name: "ShippingAmountInNoor",
                table: "CourseCarts");

            migrationBuilder.DropColumn(
                name: "ItemType",
                table: "CourseCartPurchaseAllocations");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "CourseCartItems");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CourseCartPurchaseAllocations",
                table: "CourseCartPurchaseAllocations",
                columns: new[] { "CartId", "ExternalCourseId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_CourseCartItems",
                table: "CourseCartItems",
                columns: new[] { "CartId", "ExternalCourseId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_CourseCarts_Discounts",
                table: "CourseCarts",
                sql: "\"ItemDiscountInNoor\" >= 0 AND \"CouponDiscountInNoor\" >= 0 AND trunc(\"ItemDiscountInNoor\") = \"ItemDiscountInNoor\" AND trunc(\"CouponDiscountInNoor\") = \"CouponDiscountInNoor\"");
        }
    }
}
