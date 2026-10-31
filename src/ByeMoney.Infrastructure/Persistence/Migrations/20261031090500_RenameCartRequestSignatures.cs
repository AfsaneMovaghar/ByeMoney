using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByeMoney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameCartRequestSignatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // نام ستون با محتوای واقعی آن هم‌راستا می‌شود: اثر انگشت اقلام درخواستی، نه فهرست شناسهٔ دوره.
            migrationBuilder.RenameColumn(
                name: "ExternalCourseIds",
                table: "CourseCarts",
                newName: "RequestedItemSignatures");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RequestedItemSignatures",
                table: "CourseCarts",
                newName: "ExternalCourseIds");
        }
    }
}
