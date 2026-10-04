using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGatewayFinancialReviewMarker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewCaseId",
                table: "TopUpRequests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewOpenedAtUtc",
                table: "TopUpRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewOutcomeCode",
                table: "TopUpRequests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewReasonCode",
                table: "TopUpRequests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewResolutionFinancialReferenceId",
                table: "TopUpRequests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewResolvedAtUtc",
                table: "TopUpRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopUpRequests_ReviewCaseId",
                table: "TopUpRequests",
                column: "ReviewCaseId",
                unique: true,
                filter: "\"ReviewCaseId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TopUpRequests_ReviewCaseId",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "ReviewCaseId",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "ReviewOpenedAtUtc",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "ReviewOutcomeCode",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "ReviewReasonCode",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "ReviewResolutionFinancialReferenceId",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "ReviewResolvedAtUtc",
                table: "TopUpRequests");
        }
    }
}
