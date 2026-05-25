using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddPageRouteTranslations : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "PageRouteTranslations",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					PageId = table.Column<int>(type: "integer", nullable: false),
					LanguageCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
					TranslatedRoute = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_PageRouteTranslations", x => x.Id);
					table.ForeignKey(
						name: "FK_PageRouteTranslations_Pages_PageId",
						column: x => x.PageId,
						principalTable: "Pages",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "IX_PageRouteTranslations_PageId_LanguageCode",
				table: "PageRouteTranslations",
				columns: new[] { "PageId", "LanguageCode" },
				unique: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "PageRouteTranslations");
		}
	}
}
