using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecordGatewayResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GatewayEventId",
                table: "TopUpRequests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GatewayResultAtUtc",
                table: "TopUpRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayResultCode",
                table: "TopUpRequests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayResultKind",
                table: "TopUpRequests",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GatewayEventId",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "GatewayResultAtUtc",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "GatewayResultCode",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "GatewayResultKind",
                table: "TopUpRequests");
        }
    }
}
