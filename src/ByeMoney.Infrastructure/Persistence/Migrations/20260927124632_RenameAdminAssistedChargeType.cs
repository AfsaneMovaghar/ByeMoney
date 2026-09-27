using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameAdminAssistedChargeType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"TopUpRequests\" SET \"ChargeType\" = 'AdminAssistedCardToCard' WHERE \"ChargeType\" = 'AdminCardToCardForDisabledUser';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"TopUpRequests\" SET \"ChargeType\" = 'AdminCardToCardForDisabledUser' WHERE \"ChargeType\" = 'AdminAssistedCardToCard';");
        }
    }
}
