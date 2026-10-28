using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminGiftNoorAndGiftBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GiftBalance",
                table: "Wallets",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "BalanceType",
                table: "LedgerEntries",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "GiftGrantBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestPayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AmountNoorPerUser = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalRecipients = table.Column<int>(type: "integer", nullable: false),
                    TotalAmountNoor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftGrantBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GiftGrantRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalUserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AmountNoor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NewGiftBalance = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftGrantRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GiftGrantRecipients_GiftGrantBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "GiftGrantBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Code", "CreatedAtUtc", "Description", "UpdatedAt" },
                values: new object[] { new Guid("77777777-7777-7777-7777-777777777777"), "Noor.Gift", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Permission to grant gift Noor currency to users", null });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId", "CreatedAt" },
                values: new object[] { new Guid("77777777-7777-7777-7777-777777777777"), new Guid("11111111-1111-1111-1111-111111111111"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_BalanceType",
                table: "LedgerEntries",
                column: "BalanceType");

            migrationBuilder.CreateIndex(
                name: "IX_GiftGrantBatches_IdempotencyKey",
                table: "GiftGrantBatches",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GiftGrantRecipients_BatchId",
                table: "GiftGrantRecipients",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_GiftGrantRecipients_ExternalUserId",
                table: "GiftGrantRecipients",
                column: "ExternalUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GiftGrantRecipients_UserId",
                table: "GiftGrantRecipients",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GiftGrantRecipients");

            migrationBuilder.DropTable(
                name: "GiftGrantBatches");

            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_BalanceType",
                table: "LedgerEntries");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("77777777-7777-7777-7777-777777777777"), new Guid("11111111-1111-1111-1111-111111111111") });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"));

            migrationBuilder.DropColumn(
                name: "GiftBalance",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "BalanceType",
                table: "LedgerEntries");
        }
    }
}
