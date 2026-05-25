using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddRegistrationInvites : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "RegistrationInvites",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "uuid", nullable: false),
					Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
					NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
					FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
					LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
					LinkHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
					CodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
					FailedAttemptCount = table.Column<int>(type: "integer", nullable: false),
					ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					ConsumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
					RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
					CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
					Locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
					FaceId = table.Column<int>(type: "integer", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_RegistrationInvites", x => x.Id);
				});

			migrationBuilder.CreateIndex(
				name: "IX_RegistrationInvites_ExpiresAtUtc",
				table: "RegistrationInvites",
				column: "ExpiresAtUtc");

			migrationBuilder.CreateIndex(
				name: "IX_RegistrationInvites_LinkHash",
				table: "RegistrationInvites",
				column: "LinkHash",
				unique: true);

			migrationBuilder.CreateIndex(
				name: "IX_RegistrationInvites_NormalizedEmail",
				table: "RegistrationInvites",
				column: "NormalizedEmail",
				unique: true,
				filter: "\"ConsumedAtUtc\" IS NULL AND \"RevokedAtUtc\" IS NULL");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "RegistrationInvites");
		}
	}
}
