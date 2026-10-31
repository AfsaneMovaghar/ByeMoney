using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameCartTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // نام جدول با محتوای واقعی سبد هم‌راستا می‌شود؛ سبد علاوه بر دوره، فصل و کالای فیزیکی را هم نگه می‌دارد.
            migrationBuilder.RenameTable(
                name: "CourseCarts",
                newName: "Carts");

            migrationBuilder.RenameTable(
                name: "CourseCartItems",
                newName: "CartItems");

            migrationBuilder.RenameIndex(
                name: "IX_CourseCarts_BuyerId",
                table: "Carts",
                newName: "IX_Carts_BuyerId");

            migrationBuilder.RenameIndex(
                name: "IX_CourseCarts_Synchronized_UpdatedAtUtc",
                table: "Carts",
                newName: "IX_Carts_Synchronized_UpdatedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_CourseCarts_TopUpRequestId",
                table: "Carts",
                newName: "IX_Carts_TopUpRequestId");

            // EF Core برای تغییر نام قیدها API ندارد. در PostgreSQL تغییر نام قید کلید اصلی، ایندکس پشت آن را هم
            // تغییر می‌دهد؛ پس همین دستورها جای حذف و ساخت دوباره قید را می‌گیرند و هیچ جدولی بازسازی نمی‌شود.
            migrationBuilder.Sql("ALTER TABLE \"Carts\" RENAME CONSTRAINT \"PK_CourseCarts\" TO \"PK_Carts\"");
            migrationBuilder.Sql("ALTER TABLE \"Carts\" RENAME CONSTRAINT \"CK_CourseCarts_Discounts\" TO \"CK_Carts_Discounts\"");

            migrationBuilder.Sql("ALTER TABLE \"CartItems\" RENAME CONSTRAINT \"PK_CourseCartItems\" TO \"PK_CartItems\"");
            migrationBuilder.Sql("ALTER TABLE \"CartItems\" RENAME CONSTRAINT \"FK_CourseCartItems_CourseCarts_CartId\" TO \"FK_CartItems_Carts_CartId\"");
            migrationBuilder.Sql("ALTER TABLE \"CartItems\" RENAME CONSTRAINT \"CK_CourseCartItems_NonNegativeAmounts\" TO \"CK_CartItems_NonNegativeAmounts\"");
            migrationBuilder.Sql("ALTER TABLE \"CartItems\" RENAME CONSTRAINT \"CK_CourseCartItems_WholeNoorAmounts\" TO \"CK_CartItems_WholeNoorAmounts\"");
            migrationBuilder.Sql("ALTER TABLE \"CartItems\" RENAME CONSTRAINT \"CK_CourseCartItems_Quantity\" TO \"CK_CartItems_Quantity\"");
            migrationBuilder.Sql("ALTER TABLE \"CartItems\" RENAME CONSTRAINT \"CK_CourseCartItems_Allocation\" TO \"CK_CartItems_Allocation\"");
            migrationBuilder.Sql("ALTER TABLE \"CartItems\" RENAME CONSTRAINT \"CK_CourseCartItems_Shares\" TO \"CK_CartItems_Shares\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"CartItems\" RENAME CONSTRAINT \"CK_CartItems_Shares\" TO \"CK_CourseCartItems_Shares\"");
            migrationBuilder.Sql("ALTER TABLE \"CartItems\" RENAME CONSTRAINT \"CK_CartItems_Allocation\" TO \"CK_CourseCartItems_Allocation\"");
            migrationBuilder.Sql("ALTER TABLE \"CartItems\" RENAME CONSTRAINT \"CK_CartItems_Quantity\" TO \"CK_CourseCartItems_Quantity\"");
            migrationBuilder.Sql("ALTER TABLE \"CartItems\" RENAME CONSTRAINT \"CK_CartItems_WholeNoorAmounts\" TO \"CK_CourseCartItems_WholeNoorAmounts\"");
            migrationBuilder.Sql("ALTER TABLE \"CartItems\" RENAME CONSTRAINT \"CK_CartItems_NonNegativeAmounts\" TO \"CK_CourseCartItems_NonNegativeAmounts\"");
            migrationBuilder.Sql("ALTER TABLE \"CartItems\" RENAME CONSTRAINT \"FK_CartItems_Carts_CartId\" TO \"FK_CourseCartItems_CourseCarts_CartId\"");
            migrationBuilder.Sql("ALTER TABLE \"CartItems\" RENAME CONSTRAINT \"PK_CartItems\" TO \"PK_CourseCartItems\"");

            migrationBuilder.Sql("ALTER TABLE \"Carts\" RENAME CONSTRAINT \"CK_Carts_Discounts\" TO \"CK_CourseCarts_Discounts\"");
            migrationBuilder.Sql("ALTER TABLE \"Carts\" RENAME CONSTRAINT \"PK_Carts\" TO \"PK_CourseCarts\"");

            migrationBuilder.RenameIndex(
                name: "IX_Carts_TopUpRequestId",
                table: "Carts",
                newName: "IX_CourseCarts_TopUpRequestId");

            migrationBuilder.RenameIndex(
                name: "IX_Carts_Synchronized_UpdatedAtUtc",
                table: "Carts",
                newName: "IX_CourseCarts_Synchronized_UpdatedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_Carts_BuyerId",
                table: "Carts",
                newName: "IX_CourseCarts_BuyerId");

            migrationBuilder.RenameTable(
                name: "CartItems",
                newName: "CourseCartItems");

            migrationBuilder.RenameTable(
                name: "Carts",
                newName: "CourseCarts");
        }
    }
}
