using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientReferenceCodeToTopUpRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientReferenceCode",
                table: "TopUpRequests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_TopUpRequests_ClientReferenceCode",
                table: "TopUpRequests",
                column: "ClientReferenceCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TopUpRequests_ClientReferenceCode",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "ClientReferenceCode",
                table: "TopUpRequests");
        }
    }
}
