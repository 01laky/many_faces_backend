using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddUserPushDevices : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "UserPushDevices",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					Platform = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
					RegistrationToken = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
					InstallationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
					CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_UserPushDevices", x => x.Id);
					table.ForeignKey(
						name: "FK_UserPushDevices_AspNetUsers_UserId",
						column: x => x.UserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "IX_UserPushDevices_RegistrationToken",
				table: "UserPushDevices",
				column: "RegistrationToken",
				unique: true);

			migrationBuilder.CreateIndex(
				name: "IX_UserPushDevices_UserId",
				table: "UserPushDevices",
				column: "UserId");

			migrationBuilder.CreateIndex(
				name: "IX_UserPushDevices_UserId_InstallationId",
				table: "UserPushDevices",
				columns: new[] { "UserId", "InstallationId" },
				unique: true,
				filter: "\"InstallationId\" IS NOT NULL");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "UserPushDevices");
		}
	}
}
