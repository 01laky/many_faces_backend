using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOperatorAiPublicStatsSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperatorAiPublicStatsSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicStatsMode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LiveMaxParallelBundleCalls = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorAiPublicStatsSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "OperatorAiPublicStatsSettings",
                columns: new[] { "Id", "PublicStatsMode", "LiveMaxParallelBundleCalls", "UpdatedAtUtc", "UpdatedByUserId" },
                values: new object[] { 1, "inline", 2, new DateTime(2026, 5, 21, 0, 0, 0, DateTimeKind.Utc), null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperatorAiPublicStatsSettings");
        }
    }
}
