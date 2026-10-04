using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManualGatewayReviewAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManualRefundReference",
                table: "TopUpRequests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewAudit",
                table: "TopUpRequests",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "ReviewRevision",
                table: "TopUpRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);
            migrationBuilder.Sql("UPDATE \"TopUpRequests\" SET \"ReviewRevision\" = 1 WHERE \"ReviewCaseId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManualRefundReference",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "ReviewAudit",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "ReviewRevision",
                table: "TopUpRequests");
        }
    }
}
