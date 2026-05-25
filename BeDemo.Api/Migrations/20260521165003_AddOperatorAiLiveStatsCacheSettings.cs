using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddOperatorAiLiveStatsCacheSettings : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "OperatorAiLiveStatsCacheSettings",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					TtlMilliseconds = table.Column<long>(type: "bigint", nullable: false),
					UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					UpdatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_OperatorAiLiveStatsCacheSettings", x => x.Id);
				});

			migrationBuilder.InsertData(
				table: "OperatorAiLiveStatsCacheSettings",
				columns: new[] { "Id", "TtlMilliseconds", "UpdatedAtUtc", "UpdatedByUserId" },
				values: new object[] { 1, 300_000L, new DateTime(2026, 5, 21, 0, 0, 0, DateTimeKind.Utc), null });
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "OperatorAiLiveStatsCacheSettings");
		}
	}
}
