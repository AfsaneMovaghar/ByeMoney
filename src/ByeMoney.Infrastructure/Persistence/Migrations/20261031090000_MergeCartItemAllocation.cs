using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MergeCartItemAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // مبلغ تخصیصی هر قلم از جدول جدا به خود ردیف سبد منتقل می‌شود. دادهٔ سبد پیش از اعمال پاک
            // می‌شود، پس هیچ مقدار پیش‌فرض یا کپی حدسی نوشته نمی‌شود؛ اگر ردیفی باقی مانده باشد،
            // اجباری‌شدن ستون با خطا متوقف می‌شود.
            migrationBuilder.AddColumn<decimal>(
                name: "AllocatedAmountInNoor",
                table: "CourseCartItems",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GiftAmountInNoor",
                table: "CourseCartItems",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RegularAmountInNoor",
                table: "CourseCartItems",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "AllocatedAmountInNoor",
                table: "CourseCartItems",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CourseCartItems_Allocation",
                table: "CourseCartItems",
                sql: "\"AllocatedAmountInNoor\" >= 0 AND trunc(\"AllocatedAmountInNoor\") = \"AllocatedAmountInNoor\" AND \"AllocatedAmountInNoor\" <= \"Quantity\" * \"PriceInNoor\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CourseCartItems_Shares",
                table: "CourseCartItems",
                sql: "(\"RegularAmountInNoor\" IS NULL AND \"GiftAmountInNoor\" IS NULL) OR (\"RegularAmountInNoor\" >= 0 AND \"GiftAmountInNoor\" >= 0 AND trunc(\"RegularAmountInNoor\") = \"RegularAmountInNoor\" AND trunc(\"GiftAmountInNoor\") = \"GiftAmountInNoor\" AND \"RegularAmountInNoor\" + \"GiftAmountInNoor\" = \"AllocatedAmountInNoor\")");

            migrationBuilder.DropTable(
                name: "CourseCartPurchaseAllocations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CourseCartPurchaseAllocations",
                columns: table => new
                {
                    CartId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalCourseId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ItemType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Course"),
                    AmountInNoor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseCartPurchaseAllocations", x => new { x.CartId, x.ExternalCourseId, x.ItemType });
                    table.CheckConstraint("CK_CourseCartPurchaseAllocations_Amount", "\"AmountInNoor\" >= 0 AND trunc(\"AmountInNoor\") = \"AmountInNoor\"");
                    table.ForeignKey(
                        name: "FK_CourseCartPurchaseAllocations_CourseCarts_CartId",
                        column: x => x.CartId,
                        principalTable: "CourseCarts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // بازگشت، مبلغ منتقل‌شده را از ردیف سبد به جدول تخصیص برمی‌گرداند و مقدار جدید نمی‌سازد.
            migrationBuilder.Sql(
                "INSERT INTO \"CourseCartPurchaseAllocations\" (\"CartId\", \"ExternalCourseId\", \"ItemType\", \"AmountInNoor\") " +
                "SELECT \"CartId\", \"ExternalCourseId\", \"ItemType\", \"AllocatedAmountInNoor\" FROM \"CourseCartItems\"");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CourseCartItems_Shares",
                table: "CourseCartItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CourseCartItems_Allocation",
                table: "CourseCartItems");

            migrationBuilder.DropColumn(
                name: "GiftAmountInNoor",
                table: "CourseCartItems");

            migrationBuilder.DropColumn(
                name: "RegularAmountInNoor",
                table: "CourseCartItems");

            migrationBuilder.DropColumn(
                name: "AllocatedAmountInNoor",
                table: "CourseCartItems");
        }
    }
}
