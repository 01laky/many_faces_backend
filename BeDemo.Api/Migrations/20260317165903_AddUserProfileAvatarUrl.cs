using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddUserProfileAvatarUrl : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "AvatarUrl",
				table: "UserProfiles",
				type: "text",
				nullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "UserId",
				table: "UserFaceRoles",
				type: "character varying(450)",
				maxLength: 450,
				nullable: false,
				oldClrType: typeof(string),
				oldType: "text");

			migrationBuilder.CreateIndex(
				name: "IX_UserFaceRoles_UserId_FaceId",
				table: "UserFaceRoles",
				columns: new[] { "UserId", "FaceId" },
				unique: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "IX_UserFaceRoles_UserId_FaceId",
				table: "UserFaceRoles");

			migrationBuilder.DropColumn(
				name: "AvatarUrl",
				table: "UserProfiles");

			migrationBuilder.AlterColumn<string>(
				name: "UserId",
				table: "UserFaceRoles",
				type: "text",
				nullable: false,
				oldClrType: typeof(string),
				oldType: "character varying(450)",
				oldMaxLength: 450);
		}
	}
}
