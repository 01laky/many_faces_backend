using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class RemoveDetailEditCreatePageTypes : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(
				"""
                DELETE FROM "Pages" WHERE "PageTypeId" IN (SELECT "Id" FROM "PageTypes" WHERE "Index" IN ('detail','edit','create'));
                DELETE FROM "PageTypes" WHERE "Index" IN ('detail','edit','create');
                """);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			// Data-only migration.
		}
	}
}
