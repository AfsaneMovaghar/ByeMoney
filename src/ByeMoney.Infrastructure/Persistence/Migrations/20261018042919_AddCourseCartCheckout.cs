using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseCartCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_CoursePurchases_BuyerCourse\" ON \"CoursePurchases\" (\"UserId\", \"ExternalProductId\")");
            migrationBuilder.AddColumn<Guid>(
                name: "CartId",
                table: "CoursePurchases",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CourseCarts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerExternalUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExternalCourseIds = table.Column<string[]>(type: "text[]", nullable: false),
                    CouponCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Items = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TopUpRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Synchronized = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseCarts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoursePurchases_CartId",
                table: "CoursePurchases",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseCarts_BuyerId",
                table: "CourseCarts",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseCarts_Synchronized_UpdatedAtUtc",
                table: "CourseCarts",
                columns: new[] { "Synchronized", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CourseCarts_TopUpRequestId",
                table: "CourseCarts",
                column: "TopUpRequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX \"IX_CoursePurchases_BuyerCourse\"");
            migrationBuilder.DropTable(
                name: "CourseCarts");

            migrationBuilder.DropIndex(
                name: "IX_CoursePurchases_CartId",
                table: "CoursePurchases");

            migrationBuilder.DropColumn(
                name: "CartId",
                table: "CoursePurchases");
        }
    }
}

