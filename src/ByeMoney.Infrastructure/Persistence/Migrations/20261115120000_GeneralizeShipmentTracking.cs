using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GeneralizeShipmentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalProductPurchases_TrackingProvider",
                table: "PhysicalProductPurchases");

            // تغییر نوع ستون به متن و بازگشت آن در Down به تبدیل صریح Postgres نیاز دارد و EF آن را تولید نمی‌کند.
            migrationBuilder.Sql(
                "ALTER TABLE \"PhysicalProductPurchases\" " +
                "ALTER COLUMN \"ProviderParcelId\" TYPE character varying(50) USING \"ProviderParcelId\"::text;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalProductPurchases_TrackingProvider",
                table: "PhysicalProductPurchases",
                sql: "\"TrackingProvider\" IS NULL OR \"TrackingProvider\" IN ('PostEx', 'Manual')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalProductPurchases_TrackingProvider",
                table: "PhysicalProductPurchases");

            // مقادیر غیرعددی در بازگشت نوع معنا ندارند؛ داده‌های محیط توسعه نگه‌داشته نمی‌شوند.
            migrationBuilder.Sql(
                "UPDATE \"PhysicalProductPurchases\" SET \"ProviderParcelId\" = NULL " +
                "WHERE \"ProviderParcelId\" IS NOT NULL AND \"ProviderParcelId\" !~ '^[0-9]+$';");

            migrationBuilder.Sql(
                "ALTER TABLE \"PhysicalProductPurchases\" " +
                "ALTER COLUMN \"ProviderParcelId\" TYPE bigint USING \"ProviderParcelId\"::bigint;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalProductPurchases_TrackingProvider",
                table: "PhysicalProductPurchases",
                sql: "\"TrackingProvider\" IS NULL OR \"TrackingProvider\" IN ('PostEx', 'Manual')");
        }
    }
}
