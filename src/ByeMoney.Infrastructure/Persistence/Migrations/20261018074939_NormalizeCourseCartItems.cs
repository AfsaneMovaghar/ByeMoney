using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeCourseCartItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // پاک‌سازی سبدهای تستی طبق تأیید مالک؛ سوابق خرید، شارژ و دفترکل حذف نمی‌شوند.
            migrationBuilder.Sql("DELETE FROM \"CourseCarts\"");

            migrationBuilder.DropColumn(
                name: "Items",
                table: "CourseCarts");

            migrationBuilder.CreateTable(
                name: "CourseCartItems",
                columns: table => new
                {
                    ExternalCourseId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CartId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    BasePriceInNoor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ItemDiscountInNoor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CouponDiscountInNoor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PriceInNoor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseCartItems", x => new { x.CartId, x.ExternalCourseId });
                    table.CheckConstraint("CK_CourseCartItems_NonNegativeAmounts", "\"BasePriceInNoor\" >= 0 AND \"ItemDiscountInNoor\" >= 0 AND \"CouponDiscountInNoor\" >= 0 AND \"PriceInNoor\" >= 0");
                    table.CheckConstraint("CK_CourseCartItems_PriceBreakdown", "\"BasePriceInNoor\" - \"ItemDiscountInNoor\" - \"CouponDiscountInNoor\" = \"PriceInNoor\"");
                    table.CheckConstraint("CK_CourseCartItems_WholeNoorAmounts", "trunc(\"BasePriceInNoor\") = \"BasePriceInNoor\" AND trunc(\"ItemDiscountInNoor\") = \"ItemDiscountInNoor\" AND trunc(\"CouponDiscountInNoor\") = \"CouponDiscountInNoor\" AND trunc(\"PriceInNoor\") = \"PriceInNoor\"");
                    table.ForeignKey(
                        name: "FK_CourseCartItems_CourseCarts_CartId",
                        column: x => x.CartId,
                        principalTable: "CourseCarts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Items",
                table: "CourseCarts",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.Sql("""
                UPDATE "CourseCarts" c SET "Items" = (
                    SELECT COALESCE(jsonb_agg(to_jsonb(i) - 'CartId' ORDER BY i."ExternalCourseId" COLLATE "C"), '[]'::jsonb)
                    FROM "CourseCartItems" i WHERE i."CartId" = c."Id"
                )
                """);
            migrationBuilder.DropTable(name: "CourseCartItems");
        }
    }
}
