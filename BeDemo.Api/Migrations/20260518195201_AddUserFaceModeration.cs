using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddUserFaceModeration : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "UserFaceModerations",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					UserId = table.Column<string>(type: "text", nullable: false),
					FaceId = table.Column<int>(type: "integer", nullable: false),
					BannedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					BannedByUserId = table.Column<string>(type: "text", nullable: false),
					Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
					LiftedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_UserFaceModerations", x => x.Id);
					table.ForeignKey(
						name: "FK_UserFaceModerations_AspNetUsers_BannedByUserId",
						column: x => x.BannedByUserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict);
					table.ForeignKey(
						name: "FK_UserFaceModerations_AspNetUsers_UserId",
						column: x => x.UserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_UserFaceModerations_Faces_FaceId",
						column: x => x.FaceId,
						principalTable: "Faces",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "IX_UserFaceModerations_BannedByUserId",
				table: "UserFaceModerations",
				column: "BannedByUserId");

			migrationBuilder.CreateIndex(
				name: "IX_UserFaceModerations_FaceId",
				table: "UserFaceModerations",
				column: "FaceId");

			migrationBuilder.CreateIndex(
				name: "IX_UserFaceModerations_UserId_FaceId_LiftedAt",
				table: "UserFaceModerations",
				columns: new[] { "UserId", "FaceId", "LiftedAt" });
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "UserFaceModerations");
		}
	}
}
