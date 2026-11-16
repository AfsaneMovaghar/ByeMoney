using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ByeMoney.Infrastructure.Persistence;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261116120000_AddCartShippingOriginId")]
    public partial class AddCartShippingOriginId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShippingOriginId",
                table: "Carts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShippingOriginId",
                table: "Carts");
        }
    }
}
