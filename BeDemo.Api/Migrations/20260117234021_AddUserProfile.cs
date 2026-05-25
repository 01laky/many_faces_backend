using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddUserProfile : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "UserProfiles",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					Nickname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
					Age = table.Column<int>(type: "integer", nullable: true),
					Rod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_UserProfiles", x => x.Id);
					table.ForeignKey(
						name: "FK_UserProfiles_AspNetUsers_UserId",
						column: x => x.UserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "IX_UserProfiles_UserId",
				table: "UserProfiles",
				column: "UserId",
				unique: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "UserProfiles");
		}
	}
}
