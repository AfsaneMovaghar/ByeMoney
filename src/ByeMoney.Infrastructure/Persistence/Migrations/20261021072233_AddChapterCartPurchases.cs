using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChapterCartPurchases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalParentId",
                table: "CoursePurchases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemType",
                table: "CoursePurchases",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Course");

            migrationBuilder.AddColumn<string>(
                name: "ItemType",
                table: "CourseCartItems",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Course");

            migrationBuilder.AddColumn<string>(
                name: "ParentExternalId",
                table: "CourseCartItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // ایندکس از مهاجرت اولیه وجود دارد؛ EF ساخت شرطی ایندکس را پشتیبانی نمی‌کند.
            // این DDL ثابت، دیتابیس فاقد ایندکس را نیز بدون حذف داده اصلاح می‌کند.
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS " +
                "\"IX_CoursePurchases_UserId_ExternalProductId\" ON \"CoursePurchases\" (\"UserId\", \"ExternalProductId\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalParentId",
                table: "CoursePurchases");

            migrationBuilder.DropColumn(
                name: "ItemType",
                table: "CoursePurchases");

            migrationBuilder.DropColumn(
                name: "ItemType",
                table: "CourseCartItems");

            migrationBuilder.DropColumn(
                name: "ParentExternalId",
                table: "CourseCartItems");
        }
    }
}
