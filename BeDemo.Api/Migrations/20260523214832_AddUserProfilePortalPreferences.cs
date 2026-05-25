using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddUserProfilePortalPreferences : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<int>(
				name: "LastSelectedFaceId",
				table: "UserProfiles",
				type: "integer",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "PreferredUiLanguage",
				table: "UserProfiles",
				type: "character varying(8)",
				maxLength: 8,
				nullable: true);

			migrationBuilder.CreateIndex(
				name: "IX_UserProfiles_LastSelectedFaceId",
				table: "UserProfiles",
				column: "LastSelectedFaceId");

			migrationBuilder.AddForeignKey(
				name: "FK_UserProfiles_Faces_LastSelectedFaceId",
				table: "UserProfiles",
				column: "LastSelectedFaceId",
				principalTable: "Faces",
				principalColumn: "Id",
				onDelete: ReferentialAction.SetNull);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropForeignKey(
				name: "FK_UserProfiles_Faces_LastSelectedFaceId",
				table: "UserProfiles");

			migrationBuilder.DropIndex(
				name: "IX_UserProfiles_LastSelectedFaceId",
				table: "UserProfiles");

			migrationBuilder.DropColumn(
				name: "LastSelectedFaceId",
				table: "UserProfiles");

			migrationBuilder.DropColumn(
				name: "PreferredUiLanguage",
				table: "UserProfiles");
		}
	}
}
