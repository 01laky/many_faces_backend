using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddUserFollows : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "UserFollows",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					FollowerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					FollowedId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_UserFollows", x => x.Id);
					table.ForeignKey(
						name: "FK_UserFollows_AspNetUsers_FollowedId",
						column: x => x.FollowedId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_UserFollows_AspNetUsers_FollowerId",
						column: x => x.FollowerId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "IX_UserFollows_FollowedId",
				table: "UserFollows",
				column: "FollowedId");

			migrationBuilder.CreateIndex(
				name: "IX_UserFollows_FollowerId_FollowedId",
				table: "UserFollows",
				columns: new[] { "FollowerId", "FollowedId" },
				unique: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "UserFollows");
		}
	}
}
