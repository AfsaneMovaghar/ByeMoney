using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNoorWithdrawals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_LedgerEntries_ReferenceType",
                table: "LedgerEntries");

            migrationBuilder.CreateTable(
                name: "NoorWithdrawals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RelatedTopUpId = table.Column<Guid>(type: "uuid", nullable: true),
                    AmountNoor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AmountRial = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    BeneficiaryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BeneficiaryCardNumber = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    BeneficiaryIban = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PaymentReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReceiptId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PaidByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaidAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReversalReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReversalTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoorWithdrawals", x => x.Id);
                    table.CheckConstraint("CK_NoorWithdrawals_Amounts_Positive", "\"AmountNoor\" > 0 AND \"AmountRial\" > 0");
                    table.CheckConstraint("CK_NoorWithdrawals_BeneficiaryAccount", "\"BeneficiaryCardNumber\" IS NOT NULL OR \"BeneficiaryIban\" IS NOT NULL");
                    table.CheckConstraint("CK_NoorWithdrawals_Kind", "\"Kind\" IN ('AdminDiscretionary', 'DuplicatePaymentRefund')");
                    table.CheckConstraint("CK_NoorWithdrawals_PaidEvidence", "\"Status\" <> 'Paid' OR (\"PaymentReference\" IS NOT NULL AND \"ReceiptId\" IS NOT NULL AND \"PaidByUserId\" IS NOT NULL AND \"PaidAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_NoorWithdrawals_RelatedTopUp", "(\"Kind\" = 'DuplicatePaymentRefund') = (\"RelatedTopUpId\" IS NOT NULL)");
                    table.CheckConstraint("CK_NoorWithdrawals_ReversalEvidence", "\"Status\" <> 'Reversed' OR (\"ReversalReason\" IS NOT NULL AND \"ReversalTransactionId\" IS NOT NULL AND \"ReversedByUserId\" IS NOT NULL AND \"ReversedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_NoorWithdrawals_Status", "\"Status\" IN ('PendingPayment', 'Paid', 'Reversed')");
                    table.ForeignKey(
                        name: "FK_NoorWithdrawals_TopUpRequests_RelatedTopUpId",
                        column: x => x.RelatedTopUpId,
                        principalTable: "TopUpRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoorWithdrawals_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoorWithdrawals_Users_PaidByUserId",
                        column: x => x.PaidByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoorWithdrawals_Users_ReversedByUserId",
                        column: x => x.ReversedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoorWithdrawals_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Code", "CreatedAtUtc", "Description", "UpdatedAt" },
                values: new object[] { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Noor.Withdraw", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Permission to withdraw regular Noor and record the manual bank payment", null });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId", "CreatedAt" },
                values: new object[] { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), new Guid("11111111-1111-1111-1111-111111111111"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.AddCheckConstraint(
                name: "CK_LedgerEntries_ReferenceType",
                table: "LedgerEntries",
                sql: "\"ReferenceType\" IN ('TopUp', 'Purchase', 'Refund', 'AdminGrant', 'WelcomeGift', 'Withdrawal', 'WithdrawalReversal')");

            migrationBuilder.CreateIndex(
                name: "IX_NoorWithdrawals_CreatedByUserId",
                table: "NoorWithdrawals",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NoorWithdrawals_OperationId",
                table: "NoorWithdrawals",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NoorWithdrawals_PaidByUserId",
                table: "NoorWithdrawals",
                column: "PaidByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NoorWithdrawals_ReceiptId",
                table: "NoorWithdrawals",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_NoorWithdrawals_RelatedTopUpId",
                table: "NoorWithdrawals",
                column: "RelatedTopUpId",
                unique: true,
                filter: "\"RelatedTopUpId\" IS NOT NULL AND \"Status\" <> 'Reversed'");

            migrationBuilder.CreateIndex(
                name: "IX_NoorWithdrawals_ReversedByUserId",
                table: "NoorWithdrawals",
                column: "ReversedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NoorWithdrawals_Status_CreatedAtUtc",
                table: "NoorWithdrawals",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NoorWithdrawals_UserId",
                table: "NoorWithdrawals",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NoorWithdrawals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LedgerEntries_ReferenceType",
                table: "LedgerEntries");

            // نوع Ledger خروج در Schema قبلی وجود ندارد؛ ثبت‌های آن حذف و اثرشان از مانده برداشته می‌شود
            // تا Wallet با Ledger تطبیق بماند و قید قبلی روی جدول دارای داده اعمال شود. خروج فقط نور عادی را تغییر می‌دهد.
            migrationBuilder.Sql(
                "UPDATE \"Wallets\" AS w SET \"Balance\" = w.\"Balance\" - s.\"Total\" " +
                "FROM (SELECT \"AccountId\", SUM(\"Amount\") AS \"Total\" FROM \"LedgerEntries\" " +
                "WHERE \"ReferenceType\" IN ('Withdrawal', 'WithdrawalReversal') AND \"BalanceType\" = 'Regular' " +
                "GROUP BY \"AccountId\") AS s " +
                "WHERE w.\"AccountId\" = s.\"AccountId\";");
            migrationBuilder.Sql(
                "DELETE FROM \"LedgerEntries\" WHERE \"ReferenceType\" IN ('Withdrawal', 'WithdrawalReversal');");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), new Guid("11111111-1111-1111-1111-111111111111") });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

            migrationBuilder.AddCheckConstraint(
                name: "CK_LedgerEntries_ReferenceType",
                table: "LedgerEntries",
                sql: "\"ReferenceType\" IN ('TopUp', 'Purchase', 'Refund', 'AdminGrant', 'WelcomeGift')");
        }
    }
}
