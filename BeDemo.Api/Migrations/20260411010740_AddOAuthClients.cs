using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddOAuthClients : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "OAuthClients",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					ClientId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
					SecretHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
					IsActive = table.Column<bool>(type: "boolean", nullable: false),
					CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_OAuthClients", x => x.Id);
				});

			migrationBuilder.CreateIndex(
				name: "IX_OAuthClients_ClientId",
				table: "OAuthClients",
				column: "ClientId",
				unique: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "OAuthClients");
		}
	}
}
