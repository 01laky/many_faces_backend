using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddUserBlocks : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "UserBlocks",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					BlockerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					BlockedId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_UserBlocks", x => x.Id);
					table.ForeignKey(
						name: "FK_UserBlocks_AspNetUsers_BlockedId",
						column: x => x.BlockedId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_UserBlocks_AspNetUsers_BlockerId",
						column: x => x.BlockerId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "IX_UserBlocks_BlockedId",
				table: "UserBlocks",
				column: "BlockedId");

			migrationBuilder.CreateIndex(
				name: "IX_UserBlocks_BlockerId_BlockedId",
				table: "UserBlocks",
				columns: new[] { "BlockerId", "BlockedId" },
				unique: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "UserBlocks");
		}
	}
}
