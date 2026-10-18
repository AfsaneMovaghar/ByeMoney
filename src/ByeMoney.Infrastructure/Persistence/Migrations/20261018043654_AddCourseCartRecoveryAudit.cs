using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseCartRecoveryAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncAttemptAtUtc",
                table: "CourseCarts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSyncErrorCode",
                table: "CourseCarts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SyncAttempts",
                table: "CourseCarts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSyncAttemptAtUtc",
                table: "CourseCarts");

            migrationBuilder.DropColumn(
                name: "LastSyncErrorCode",
                table: "CourseCarts");

            migrationBuilder.DropColumn(
                name: "SyncAttempts",
                table: "CourseCarts");
        }
    }
}
