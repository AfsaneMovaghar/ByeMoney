using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentSharesAndTopUpCartLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CartItems_Shares",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "TopUpRequestIds",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "GiftAmountInNoor",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "RegularAmountInNoor",
                table: "CartItems");

            migrationBuilder.AddColumn<Guid>(
                name: "CartId",
                table: "TopUpRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GiftAmountInNoor",
                table: "PhysicalProductPurchases",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RegularAmountInNoor",
                table: "PhysicalProductPurchases",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GiftAmountInNoor",
                table: "CoursePurchases",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RegularAmountInNoor",
                table: "CoursePurchases",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopUpRequests_CartId",
                table: "TopUpRequests",
                column: "CartId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalProductPurchases_PaymentShares",
                table: "PhysicalProductPurchases",
                sql: "(\"RegularAmountInNoor\" IS NULL AND \"GiftAmountInNoor\" IS NULL) OR (\"RegularAmountInNoor\" >= 0 AND \"GiftAmountInNoor\" >= 0 AND \"RegularAmountInNoor\" + \"GiftAmountInNoor\" = \"PaidAmountInNoor\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CoursePurchases_PaymentShares",
                table: "CoursePurchases",
                sql: "(\"RegularAmountInNoor\" IS NULL AND \"GiftAmountInNoor\" IS NULL) OR (\"RegularAmountInNoor\" >= 0 AND \"GiftAmountInNoor\" >= 0 AND \"RegularAmountInNoor\" + \"GiftAmountInNoor\" = \"PriceInNoorAtPurchaseTime\")");

            migrationBuilder.AddForeignKey(
                name: "FK_TopUpRequests_Carts_CartId",
                table: "TopUpRequests",
                column: "CartId",
                principalTable: "Carts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TopUpRequests_Carts_CartId",
                table: "TopUpRequests");

            migrationBuilder.DropIndex(
                name: "IX_TopUpRequests_CartId",
                table: "TopUpRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalProductPurchases_PaymentShares",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CoursePurchases_PaymentShares",
                table: "CoursePurchases");

            migrationBuilder.DropColumn(
                name: "CartId",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "GiftAmountInNoor",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "RegularAmountInNoor",
                table: "PhysicalProductPurchases");

            migrationBuilder.DropColumn(
                name: "GiftAmountInNoor",
                table: "CoursePurchases");

            migrationBuilder.DropColumn(
                name: "RegularAmountInNoor",
                table: "CoursePurchases");

            migrationBuilder.AddColumn<Guid[]>(
                name: "TopUpRequestIds",
                table: "Carts",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.AddColumn<decimal>(
                name: "GiftAmountInNoor",
                table: "CartItems",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RegularAmountInNoor",
                table: "CartItems",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CartItems_Shares",
                table: "CartItems",
                sql: "(\"RegularAmountInNoor\" IS NULL AND \"GiftAmountInNoor\" IS NULL) OR (\"RegularAmountInNoor\" >= 0 AND \"GiftAmountInNoor\" >= 0 AND trunc(\"RegularAmountInNoor\") = \"RegularAmountInNoor\" AND trunc(\"GiftAmountInNoor\") = \"GiftAmountInNoor\" AND \"RegularAmountInNoor\" + \"GiftAmountInNoor\" = \"AllocatedAmountInNoor\")");
        }
    }
}
