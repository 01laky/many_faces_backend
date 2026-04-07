using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeDemo.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveListPageType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove CMS "list" pages and PageType; children cascade from Pages (route translations, page components).
            migrationBuilder.Sql(
                """
                DELETE FROM "Pages" WHERE "PageTypeId" IN (SELECT "Id" FROM "PageTypes" WHERE "Index" = 'list');
                DELETE FROM "PageTypes" WHERE "Index" = 'list';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only migration; restoring list PageType/pages would need known IDs — re-seed if needed.
        }
    }
}
