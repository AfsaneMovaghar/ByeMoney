using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveCourseCartDiscountsToCart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CourseCartItems_NonNegativeAmounts",
                table: "CourseCartItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CourseCartItems_PriceBreakdown",
                table: "CourseCartItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CourseCartItems_WholeNoorAmounts",
                table: "CourseCartItems");

            migrationBuilder.AddColumn<decimal>(
                name: "CouponDiscountInNoor",
                table: "CourseCarts",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ItemDiscountInNoor",
                table: "CourseCarts",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "CourseCartPurchaseAllocations",
                columns: table => new
                {
                    ExternalCourseId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CartId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountInNoor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseCartPurchaseAllocations", x => new { x.CartId, x.ExternalCourseId });
                    table.CheckConstraint("CK_CourseCartPurchaseAllocations_Amount", "\"AmountInNoor\" >= 0 AND trunc(\"AmountInNoor\") = \"AmountInNoor\"");
                    table.ForeignKey(
                        name: "FK_CourseCartPurchaseAllocations_CourseCarts_CartId",
                        column: x => x.CartId,
                        principalTable: "CourseCarts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // مدل قدیمی پس از تغییر schema با EF قابل خواندن نیست؛ انتقال یک‌بارهٔ داده با SQL ثابت،
            // از ساخت DbContext تاریخی و نگهداری مدل موازی برای همین migration جلوگیری می‌کند.
            migrationBuilder.Sql("""
                INSERT INTO "CourseCartPurchaseAllocations" ("CartId", "ExternalCourseId", "AmountInNoor")
                SELECT "CartId", "ExternalCourseId", "PriceInNoor" FROM "CourseCartItems";

                UPDATE "CourseCarts" c
                SET "ItemDiscountInNoor" = totals.item_discount,
                    "CouponDiscountInNoor" = totals.coupon_discount
                FROM (
                    SELECT "CartId", sum("ItemDiscountInNoor") AS item_discount,
                        sum("CouponDiscountInNoor") AS coupon_discount
                    FROM "CourseCartItems" GROUP BY "CartId"
                ) totals WHERE c."Id" = totals."CartId";

                UPDATE "CourseCartItems" SET "PriceInNoor" = "BasePriceInNoor";
                """);

            migrationBuilder.DropColumn(
                name: "BasePriceInNoor",
                table: "CourseCartItems");

            migrationBuilder.DropColumn(
                name: "CouponDiscountInNoor",
                table: "CourseCartItems");

            migrationBuilder.DropColumn(
                name: "ItemDiscountInNoor",
                table: "CourseCartItems");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CourseCarts_Discounts",
                table: "CourseCarts",
                sql: "\"ItemDiscountInNoor\" >= 0 AND \"CouponDiscountInNoor\" >= 0 AND trunc(\"ItemDiscountInNoor\") = \"ItemDiscountInNoor\" AND trunc(\"CouponDiscountInNoor\") = \"CouponDiscountInNoor\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CourseCartItems_NonNegativeAmounts",
                table: "CourseCartItems",
                sql: "\"PriceInNoor\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CourseCartItems_WholeNoorAmounts",
                table: "CourseCartItems",
                sql: "trunc(\"PriceInNoor\") = \"PriceInNoor\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CourseCarts_Discounts",
                table: "CourseCarts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CourseCartItems_NonNegativeAmounts",
                table: "CourseCartItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CourseCartItems_WholeNoorAmounts",
                table: "CourseCartItems");

            migrationBuilder.AddColumn<decimal>(
                name: "BasePriceInNoor",
                table: "CourseCartItems",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CouponDiscountInNoor",
                table: "CourseCartItems",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ItemDiscountInNoor",
                table: "CourseCartItems",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            // بازگشت، قیمت و مبلغ پرداخت هر دوره و جمع هر نوع تخفیف را حفظ می‌کند.
            // سهم نوع تخفیف در ردیف‌های مدل قدیمی به ترتیب شناسه بازسازی می‌شود.
            migrationBuilder.Sql("""
                WITH amounts AS (
                    SELECT i."CartId", i."ExternalCourseId", i."PriceInNoor" AS base_price,
                        a."AmountInNoor" AS paid,
                        i."PriceInNoor" - a."AmountInNoor" AS discount,
                        c."ItemDiscountInNoor" AS item_discount,
                        COALESCE(sum(i."PriceInNoor" - a."AmountInNoor") OVER (
                            PARTITION BY i."CartId" ORDER BY i."ExternalCourseId" COLLATE "C"
                            ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING), 0) AS previous_discount
                    FROM "CourseCartItems" i
                    JOIN "CourseCartPurchaseAllocations" a
                        ON a."CartId" = i."CartId" AND a."ExternalCourseId" = i."ExternalCourseId"
                    JOIN "CourseCarts" c ON c."Id" = i."CartId"
                )
                UPDATE "CourseCartItems" i
                SET "BasePriceInNoor" = amounts.base_price,
                    "PriceInNoor" = amounts.paid,
                    "ItemDiscountInNoor" = LEAST(amounts.discount, GREATEST(0, amounts.item_discount - amounts.previous_discount)),
                    "CouponDiscountInNoor" = amounts.discount - LEAST(amounts.discount, GREATEST(0, amounts.item_discount - amounts.previous_discount))
                FROM amounts
                WHERE i."CartId" = amounts."CartId" AND i."ExternalCourseId" = amounts."ExternalCourseId";
                """);

            migrationBuilder.DropTable(
                name: "CourseCartPurchaseAllocations");

            migrationBuilder.DropColumn(
                name: "CouponDiscountInNoor",
                table: "CourseCarts");

            migrationBuilder.DropColumn(
                name: "ItemDiscountInNoor",
                table: "CourseCarts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CourseCartItems_NonNegativeAmounts",
                table: "CourseCartItems",
                sql: "\"BasePriceInNoor\" >= 0 AND \"ItemDiscountInNoor\" >= 0 AND \"CouponDiscountInNoor\" >= 0 AND \"PriceInNoor\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CourseCartItems_PriceBreakdown",
                table: "CourseCartItems",
                sql: "\"BasePriceInNoor\" - \"ItemDiscountInNoor\" - \"CouponDiscountInNoor\" = \"PriceInNoor\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CourseCartItems_WholeNoorAmounts",
                table: "CourseCartItems",
                sql: "trunc(\"BasePriceInNoor\") = \"BasePriceInNoor\" AND trunc(\"ItemDiscountInNoor\") = \"ItemDiscountInNoor\" AND trunc(\"CouponDiscountInNoor\") = \"CouponDiscountInNoor\" AND trunc(\"PriceInNoor\") = \"PriceInNoor\"");
        }
    }
}
