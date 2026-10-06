using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ResolveReviewedTopUpStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "TopUpRequests"
                SET "Status" = 'ManuallyRefunded'
                WHERE "Status" IN ('Pending', 'Rejected')
                  AND "ManualRefundReference" IS NOT NULL;

                UPDATE "TopUpRequests"
                SET "Status" = 'Unresolved'
                WHERE "Status" IN ('Pending', 'Rejected')
                  AND "ManualRefundReference" IS NULL
                  AND "ReviewOutcomeCode" = 'NO_MATCHING_DEPOSIT'
                  AND "ReviewResolvedAtUtc" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "TopUpRequests"
                SET "Status" = CASE WHEN "RejectedAtUtc" IS NOT NULL THEN 'Rejected' ELSE 'Pending' END
                WHERE "Status" IN ('ManuallyRefunded', 'Unresolved');
                """);
        }
    }
}
