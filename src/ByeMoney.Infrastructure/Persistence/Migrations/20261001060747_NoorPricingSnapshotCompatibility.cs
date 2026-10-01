using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NoorPricingSnapshotCompatibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "TopUpRequests" AS t
                SET "PendingItems" = converted.items
                FROM (
                    SELECT source."Id", jsonb_agg(
                        CASE WHEN item.value ? 'PriceSnapshot' AND item.value ? 'RateSnapshot'
                            THEN jsonb_set(item.value, '{PriceNoorSnapshot}',
                                to_jsonb(round((item.value->>'PriceSnapshot')::numeric /
                                    (item.value->>'RateSnapshot')::numeric, 4)), true)
                            ELSE item.value END ORDER BY item.ordinality) AS items
                    FROM "TopUpRequests" AS source,
                         jsonb_array_elements(source."PendingItems") WITH ORDINALITY AS item(value, ordinality)
                    WHERE source."PendingItems" IS NOT NULL
                      AND jsonb_typeof(source."PendingItems") = 'array'
                    GROUP BY source."Id"
                ) AS converted
                WHERE t."Id" = converted."Id";
                """);

            migrationBuilder.AlterColumn<decimal>(
                name: "PriceInRialAtPurchaseTime",
                table: "CoursePurchases",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "ConversionRateAtPurchaseTime",
                table: "CoursePurchases",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "TopUpRequests" AS t
                SET "PendingItems" = converted.items
                FROM (
                    SELECT source."Id", jsonb_agg(
                        CASE WHEN item.value ? 'PriceSnapshot' AND item.value ? 'RateSnapshot'
                            THEN item.value - 'PriceNoorSnapshot'
                            ELSE item.value END ORDER BY item.ordinality) AS items
                    FROM "TopUpRequests" AS source,
                         jsonb_array_elements(source."PendingItems") WITH ORDINALITY AS item(value, ordinality)
                    WHERE source."PendingItems" IS NOT NULL
                      AND jsonb_typeof(source."PendingItems") = 'array'
                    GROUP BY source."Id"
                ) AS converted
                WHERE t."Id" = converted."Id";
                """);

            migrationBuilder.AlterColumn<decimal>(
                name: "PriceInRialAtPurchaseTime",
                table: "CoursePurchases",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ConversionRateAtPurchaseTime",
                table: "CoursePurchases",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4,
                oldNullable: true);
        }
    }
}
