using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddOperatorAiSystemSettings : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "OperatorAiSystemSettings",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					AiEnabled = table.Column<bool>(type: "boolean", nullable: false),
					UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					UpdatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
					LastEnabledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
					LastEnableHealthStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_OperatorAiSystemSettings", x => x.Id);
				});
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "OperatorAiSystemSettings");
		}
	}
}
