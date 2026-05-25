using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddOAuthRefreshTokens : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "OAuthRefreshTokens",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
					UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					UseRememberMeAccessLifetime = table.Column<bool>(type: "boolean", nullable: false),
					RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
					ReplacedByTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_OAuthRefreshTokens", x => x.Id);
					table.ForeignKey(
						name: "FK_OAuthRefreshTokens_AspNetUsers_UserId",
						column: x => x.UserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "IX_OAuthRefreshTokens_TokenHash",
				table: "OAuthRefreshTokens",
				column: "TokenHash");

			migrationBuilder.CreateIndex(
				name: "IX_OAuthRefreshTokens_UserId",
				table: "OAuthRefreshTokens",
				column: "UserId");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "OAuthRefreshTokens");
		}
	}
}
