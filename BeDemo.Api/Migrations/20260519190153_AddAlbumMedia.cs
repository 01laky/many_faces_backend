using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddAlbumMedia : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "AlbumMedia",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					AlbumId = table.Column<int>(type: "integer", nullable: false),
					MediaType = table.Column<int>(type: "integer", nullable: false),
					ImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
					VideoUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
					ThumbnailUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
					Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
					SortOrder = table.Column<int>(type: "integer", nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_AlbumMedia", x => x.Id);
					table.ForeignKey(
						name: "FK_AlbumMedia_Albums_AlbumId",
						column: x => x.AlbumId,
						principalTable: "Albums",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "IX_AlbumMedia_AlbumId",
				table: "AlbumMedia",
				column: "AlbumId");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "AlbumMedia");
		}
	}
}
