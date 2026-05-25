using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddRoleScopeAndUserFaceRole : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// Add Scope column (0 = Global, 1 = Face), nullable for backfill
			migrationBuilder.AddColumn<int>(
				name: "Scope",
				table: "UserRoles",
				type: "integer",
				nullable: true);

			// Backfill: Global roles = 0, Face roles = 1
			migrationBuilder.Sql(@"
                UPDATE ""UserRoles"" SET ""Scope"" = 0 WHERE ""Name"" IN ('SUPER_ADMIN', 'ADMIN', 'USER', 'HOST');
                UPDATE ""UserRoles"" SET ""Scope"" = 1 WHERE ""Name"" IN ('FACE_ADMIN', 'FACE_USER', 'INZERENT', 'SUBSCRIBER', 'FACE_HOST');
            ");

			// Insert HOST (Global) and FACE_HOST (Face) if not present
			migrationBuilder.Sql(@"
                INSERT INTO ""UserRoles"" (""Name"", ""Description"", ""Scope"", ""CreatedAt"")
                SELECT 'HOST', 'Host - Global host role', 0, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""UserRoles"" WHERE ""Name"" = 'HOST');
                INSERT INTO ""UserRoles"" (""Name"", ""Description"", ""Scope"", ""CreatedAt"")
                SELECT 'FACE_HOST', 'Face Host - Default role per face', 1, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""UserRoles"" WHERE ""Name"" = 'FACE_HOST');
            ");

			// Set Scope for any remaining rows (e.g. old data)
			migrationBuilder.Sql(@"UPDATE ""UserRoles"" SET ""Scope"" = 0 WHERE ""Scope"" IS NULL;");

			migrationBuilder.AlterColumn<int>(
				name: "Scope",
				table: "UserRoles",
				type: "integer",
				nullable: false,
				defaultValue: 0,
				oldClrType: typeof(int),
				oldType: "integer",
				oldNullable: true);

			// Create UserFaceRoles table
			migrationBuilder.CreateTable(
				name: "UserFaceRoles",
				columns: table => new
				{
					UserId = table.Column<string>(type: "text", nullable: false),
					FaceId = table.Column<int>(type: "integer", nullable: false),
					UserRoleId = table.Column<int>(type: "integer", nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_UserFaceRoles", x => new { x.UserId, x.FaceId });
					table.ForeignKey(
						name: "FK_UserFaceRoles_AspNetUsers_UserId",
						column: x => x.UserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_UserFaceRoles_Faces_FaceId",
						column: x => x.FaceId,
						principalTable: "Faces",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_UserFaceRoles_UserRoles_UserRoleId",
						column: x => x.UserRoleId,
						principalTable: "UserRoles",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict);
				});

			migrationBuilder.CreateIndex(
				name: "IX_UserFaceRoles_FaceId",
				table: "UserFaceRoles",
				column: "FaceId");

			migrationBuilder.CreateIndex(
				name: "IX_UserFaceRoles_UserRoleId",
				table: "UserFaceRoles",
				column: "UserRoleId");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(name: "UserFaceRoles");

			migrationBuilder.DropColumn(
				name: "Scope",
				table: "UserRoles");
		}
	}
}
