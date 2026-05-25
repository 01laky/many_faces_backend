using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddFaceProfilesVisibilityAndSocial : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AlterColumn<bool>(
				name: "IsActive",
				table: "UserFaceProfiles",
				type: "boolean",
				nullable: false,
				oldClrType: typeof(bool),
				oldType: "boolean",
				oldDefaultValue: true);

			migrationBuilder.AddColumn<bool>(
				name: "FaceRoleIntroCompleted",
				table: "UserFaceProfiles",
				type: "boolean",
				nullable: false,
				defaultValue: false);

			migrationBuilder.AddColumn<bool>(
				name: "Visited",
				table: "UserFaceProfiles",
				type: "boolean",
				nullable: false,
				defaultValue: false);

			migrationBuilder.AddColumn<bool>(
				name: "AllowRecensions",
				table: "Faces",
				type: "boolean",
				nullable: false,
				defaultValue: false);

			migrationBuilder.AddColumn<int>(
				name: "Visibility",
				table: "Faces",
				type: "integer",
				nullable: false,
				defaultValue: 0);

			migrationBuilder.CreateTable(
				name: "UserFaceProfileComments",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					UserFaceProfileId = table.Column<int>(type: "integer", nullable: false),
					UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_UserFaceProfileComments", x => x.Id);
					table.ForeignKey(
						name: "FK_UserFaceProfileComments_AspNetUsers_UserId",
						column: x => x.UserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_UserFaceProfileComments_UserFaceProfiles_UserFaceProfileId",
						column: x => x.UserFaceProfileId,
						principalTable: "UserFaceProfiles",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "UserFaceProfileLikes",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					UserFaceProfileId = table.Column<int>(type: "integer", nullable: false),
					UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_UserFaceProfileLikes", x => x.Id);
					table.ForeignKey(
						name: "FK_UserFaceProfileLikes_AspNetUsers_UserId",
						column: x => x.UserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_UserFaceProfileLikes_UserFaceProfiles_UserFaceProfileId",
						column: x => x.UserFaceProfileId,
						principalTable: "UserFaceProfiles",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "UserFaceProfileReviews",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					UserFaceProfileId = table.Column<int>(type: "integer", nullable: false),
					AuthorUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
					Text = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
					Stars = table.Column<byte>(type: "smallint", nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_UserFaceProfileReviews", x => x.Id);
					table.ForeignKey(
						name: "FK_UserFaceProfileReviews_AspNetUsers_AuthorUserId",
						column: x => x.AuthorUserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_UserFaceProfileReviews_UserFaceProfiles_UserFaceProfileId",
						column: x => x.UserFaceProfileId,
						principalTable: "UserFaceProfiles",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "IX_UserFaceProfileComments_UserFaceProfileId",
				table: "UserFaceProfileComments",
				column: "UserFaceProfileId");

			migrationBuilder.CreateIndex(
				name: "IX_UserFaceProfileComments_UserId",
				table: "UserFaceProfileComments",
				column: "UserId");

			migrationBuilder.CreateIndex(
				name: "IX_UserFaceProfileLikes_UserFaceProfileId_UserId",
				table: "UserFaceProfileLikes",
				columns: new[] { "UserFaceProfileId", "UserId" },
				unique: true);

			migrationBuilder.CreateIndex(
				name: "IX_UserFaceProfileLikes_UserId",
				table: "UserFaceProfileLikes",
				column: "UserId");

			migrationBuilder.CreateIndex(
				name: "IX_UserFaceProfileReviews_AuthorUserId",
				table: "UserFaceProfileReviews",
				column: "AuthorUserId");

			migrationBuilder.CreateIndex(
				name: "IX_UserFaceProfileReviews_UserFaceProfileId_AuthorUserId",
				table: "UserFaceProfileReviews",
				columns: new[] { "UserFaceProfileId", "AuthorUserId" },
				unique: true);

			// Sync IsActive with face role: host = inactive in directory sense.
			// PostgreSQL: target alias (ufp) must not appear inside JOIN ON of FROM; use WHERE for ufp/ufr link.
			migrationBuilder.Sql("""
                UPDATE "UserFaceProfiles" AS ufp
                SET "IsActive" = false
                FROM "UserProfiles" AS up
                INNER JOIN "UserFaceRoles" AS ufr ON ufr."UserId" = up."UserId"
                INNER JOIN "UserRoles" AS ur ON ur."Id" = ufr."UserRoleId"
                WHERE ufp."UserProfileId" = up."Id"
                  AND ufr."FaceId" = ufp."FaceId"
                  AND ur."Name" = 'FACE_HOST';
                """);

			migrationBuilder.Sql("""
                UPDATE "UserFaceProfiles" AS ufp
                SET "IsActive" = true
                FROM "UserProfiles" AS up
                INNER JOIN "UserFaceRoles" AS ufr ON ufr."UserId" = up."UserId"
                INNER JOIN "UserRoles" AS ur ON ur."Id" = ufr."UserRoleId"
                WHERE ufp."UserProfileId" = up."Id"
                  AND ufr."FaceId" = ufp."FaceId"
                  AND ur."Name" <> 'FACE_HOST';
                """);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "UserFaceProfileComments");

			migrationBuilder.DropTable(
				name: "UserFaceProfileLikes");

			migrationBuilder.DropTable(
				name: "UserFaceProfileReviews");

			migrationBuilder.DropColumn(
				name: "FaceRoleIntroCompleted",
				table: "UserFaceProfiles");

			migrationBuilder.DropColumn(
				name: "Visited",
				table: "UserFaceProfiles");

			migrationBuilder.DropColumn(
				name: "AllowRecensions",
				table: "Faces");

			migrationBuilder.DropColumn(
				name: "Visibility",
				table: "Faces");

			migrationBuilder.AlterColumn<bool>(
				name: "IsActive",
				table: "UserFaceProfiles",
				type: "boolean",
				nullable: false,
				defaultValue: true,
				oldClrType: typeof(bool),
				oldType: "boolean");
		}
	}
}
