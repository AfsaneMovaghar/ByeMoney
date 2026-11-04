using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhysicalProductCancellations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CancelledQuantity",
                table: "PhysicalProductPurchases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundedGiftInNoor",
                table: "PhysicalProductPurchases",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundedRegularInNoor",
                table: "PhysicalProductPurchases",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "PhysicalProductCancellations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CartId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    RegularRefundInNoor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GiftRefundInNoor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ShippingRegularRefundInNoor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ShippingGiftRefundInNoor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Actor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalProductCancellations", x => x.Id);
                    table.CheckConstraint("CK_PhysicalProductCancellations_Actor", "\"Actor\" IN ('Buyer', 'Admin')");
                    table.CheckConstraint("CK_PhysicalProductCancellations_NonNegativeRefunds", "\"RegularRefundInNoor\" >= 0 AND \"GiftRefundInNoor\" >= 0 AND \"ShippingRegularRefundInNoor\" >= 0 AND \"ShippingGiftRefundInNoor\" >= 0");
                    table.CheckConstraint("CK_PhysicalProductCancellations_Quantity", "\"Quantity\" >= 1");
                    table.ForeignKey(
                        name: "FK_PhysicalProductCancellations_Carts_CartId",
                        column: x => x.CartId,
                        principalTable: "Carts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalProductCancellations_PhysicalProductPurchases_Purch~",
                        column: x => x.PurchaseId,
                        principalTable: "PhysicalProductPurchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalProductCancellations_Users_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalProductPurchases_CancelledQuantity",
                table: "PhysicalProductPurchases",
                sql: "\"CancelledQuantity\" >= 0 AND \"CancelledQuantity\" <= \"Quantity\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalProductPurchases_RefundCap",
                table: "PhysicalProductPurchases",
                sql: "\"RefundedRegularInNoor\" >= 0 AND \"RefundedGiftInNoor\" >= 0 AND \"RefundedRegularInNoor\" <= COALESCE(\"RegularAmountInNoor\", 0) AND \"RefundedGiftInNoor\" <= COALESCE(\"GiftAmountInNoor\", 0)");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalProductCancellations_CartId_ShippingRefund",
                table: "PhysicalProductCancellations",
                column: "CartId",
                unique: true,
                filter: "\"ShippingRegularRefundInNoor\" + \"ShippingGiftRefundInNoor\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalProductCancellations_OperationId_PurchaseId",
                table: "PhysicalProductCancellations",
                columns: new[] { "OperationId", "PurchaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalProductCancellations_PerformedByUserId",
                table: "PhysicalProductCancellations",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalProductCancellations_PurchaseId",
                table: "PhysicalProductCancellations",
                column: "PurchaseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhysicalProductCancellations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalProductPurchases_CancelledQuantity",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalProductPurchases_RefundCap",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "CancelledQuantity",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "RefundedGiftInNoor",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "RefundedRegularInNoor",
                table: "PhysicalProductPurchases");
        }
    }
}
