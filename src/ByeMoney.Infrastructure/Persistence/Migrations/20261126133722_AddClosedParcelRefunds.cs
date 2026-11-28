using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClosedParcelRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedParcelRefundedAtUtc",
                table: "PhysicalProductPurchases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClosedParcelRefunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CartId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RegularRefundInNoor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GiftRefundInNoor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClosedParcelRefunds", x => x.Id);
                    table.CheckConstraint("CK_ClosedParcelRefunds_NonNegativeRefunds", "\"RegularRefundInNoor\" >= 0 AND \"GiftRefundInNoor\" >= 0 AND \"RegularRefundInNoor\" + \"GiftRefundInNoor\" > 0");
                    table.CheckConstraint("CK_ClosedParcelRefunds_ScopeId", "\"ScopeId\" IN ('TARHE_ELAHI', 'BUYMONEY')");
                    table.ForeignKey(
                        name: "FK_ClosedParcelRefunds_Carts_CartId",
                        column: x => x.CartId,
                        principalTable: "Carts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClosedParcelRefunds_PhysicalProductPurchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "PhysicalProductPurchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClosedParcelRefunds_Users_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClosedParcelRefunds_CartId",
                table: "ClosedParcelRefunds",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "IX_ClosedParcelRefunds_OperationId",
                table: "ClosedParcelRefunds",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClosedParcelRefunds_PerformedByUserId",
                table: "ClosedParcelRefunds",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClosedParcelRefunds_PurchaseId",
                table: "ClosedParcelRefunds",
                column: "PurchaseId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClosedParcelRefunds");

            migrationBuilder.DropColumn(
                name: "ClosedParcelRefundedAtUtc",
                table: "PhysicalProductPurchases");
        }
    }
}
