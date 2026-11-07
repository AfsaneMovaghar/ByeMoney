using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderSettlementPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderSettlementPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OrderCount = table.Column<int>(type: "integer", nullable: false),
                    TotalNetAmountInNoor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderSettlementPeriods", x => x.Id);
                    table.CheckConstraint("CK_OrderSettlementPeriods_OrderCount", "\"OrderCount\" >= 1");
                    table.CheckConstraint("CK_OrderSettlementPeriods_TotalNetAmount", "\"TotalNetAmountInNoor\" >= 0");
                    table.ForeignKey(
                        name: "FK_OrderSettlementPeriods_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderSettlementItems",
                columns: table => new
                {
                    OrderId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BuyerExternalUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OrderCreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NetAmountInNoor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderSettlementItems", x => x.OrderId);
                    table.CheckConstraint("CK_OrderSettlementItems_NetAmount", "\"NetAmountInNoor\" >= 0");
                    table.ForeignKey(
                        name: "FK_OrderSettlementItems_OrderSettlementPeriods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "OrderSettlementPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Code", "CreatedAtUtc", "Description", "UpdatedAt" },
                values: new object[] { new Guid("99999999-9999-9999-9999-999999999999"), "Purchases.Settle", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Permission to close settlement periods of ByeMoney orders", null });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId", "CreatedAt" },
                values: new object[] { new Guid("99999999-9999-9999-9999-999999999999"), new Guid("11111111-1111-1111-1111-111111111111"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_OrderSettlementItems_PeriodId_OrderCreatedAtUtc",
                table: "OrderSettlementItems",
                columns: new[] { "PeriodId", "OrderCreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderSettlementPeriods_ClosedByUserId",
                table: "OrderSettlementPeriods",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderSettlementPeriods_CreatedAtUtc",
                table: "OrderSettlementPeriods",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderSettlementItems");

            migrationBuilder.DropTable(
                name: "OrderSettlementPeriods");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("99999999-9999-9999-9999-999999999999"), new Guid("11111111-1111-1111-1111-111111111111") });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"));
        }
    }
}
