using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseCartPaymentRetries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReservationId",
                table: "CourseCarts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid[]>(
                name: "TopUpRequestIds",
                table: "CourseCarts",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.Sql("""
                UPDATE "CourseCarts" SET "ReservationId" = "Id",
                    "TopUpRequestIds" = CASE WHEN "TopUpRequestId" IS NULL THEN ARRAY[]::uuid[]
                        ELSE ARRAY["TopUpRequestId"] END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReservationId",
                table: "CourseCarts");

            migrationBuilder.DropColumn(
                name: "TopUpRequestIds",
                table: "CourseCarts");
        }
    }
}
