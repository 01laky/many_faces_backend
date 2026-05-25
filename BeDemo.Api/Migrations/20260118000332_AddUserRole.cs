using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddUserRole : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// First create UserRoles table
			migrationBuilder.CreateTable(
				name: "UserRoles",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
					Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_UserRoles", x => x.Id);
				});

			migrationBuilder.CreateIndex(
				name: "IX_UserRoles_Name",
				table: "UserRoles",
				column: "Name",
				unique: true);

			// Seed default roles
			migrationBuilder.InsertData(
				table: "UserRoles",
				columns: new[] { "Id", "Name", "Description", "CreatedAt" },
				values: new object[,]
				{
					{ 1, "SUPER_ADMIN", "Super Administrator - Full system access", DateTime.UtcNow },
					{ 2, "ADMIN", "Administrator - Administrative access", DateTime.UtcNow },
					{ 3, "FACE_ADMIN", "Face Administrator - Manages faces and pages", DateTime.UtcNow },
					{ 4, "INZERENT", "Inzerent - Advertisement manager", DateTime.UtcNow },
					{ 5, "SUBSCRIBER", "Subscriber - Premium user access", DateTime.UtcNow },
					{ 6, "USER", "User - Standard user access", DateTime.UtcNow }
				});

			// Add UserRoleId column with default value of USER role (Id = 6)
			migrationBuilder.AddColumn<int>(
				name: "UserRoleId",
				table: "AspNetUsers",
				type: "integer",
				nullable: false,
				defaultValue: 6);

			migrationBuilder.CreateIndex(
				name: "IX_AspNetUsers_UserRoleId",
				table: "AspNetUsers",
				column: "UserRoleId");

			migrationBuilder.AddForeignKey(
				name: "FK_AspNetUsers_UserRoles_UserRoleId",
				table: "AspNetUsers",
				column: "UserRoleId",
				principalTable: "UserRoles",
				principalColumn: "Id",
				onDelete: ReferentialAction.Restrict);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropForeignKey(
				name: "FK_AspNetUsers_UserRoles_UserRoleId",
				table: "AspNetUsers");

			migrationBuilder.DropTable(
				name: "UserRoles");

			migrationBuilder.DropIndex(
				name: "IX_AspNetUsers_UserRoleId",
				table: "AspNetUsers");

			migrationBuilder.DropColumn(
				name: "UserRoleId",
				table: "AspNetUsers");
		}
	}
}
