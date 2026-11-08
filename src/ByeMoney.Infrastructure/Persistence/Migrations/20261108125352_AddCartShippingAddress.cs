using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCartShippingAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShippingAddress_City",
                table: "Carts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingAddress_FullAddress",
                table: "Carts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingAddress_PostalCode",
                table: "Carts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingAddress_Province",
                table: "Carts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingAddress_RecipientName",
                table: "Carts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingAddress_RecipientPhone",
                table: "Carts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingAddress_Version",
                table: "Carts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShippingAddress_City",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "ShippingAddress_FullAddress",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "ShippingAddress_PostalCode",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "ShippingAddress_Province",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "ShippingAddress_RecipientName",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "ShippingAddress_RecipientPhone",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "ShippingAddress_Version",
                table: "Carts");
        }
    }
}
