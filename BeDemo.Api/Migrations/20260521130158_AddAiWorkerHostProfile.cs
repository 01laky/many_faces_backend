using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddAiWorkerHostProfile : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "AiWorkerHostProfiles",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					WorkerInstanceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
					CollectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					GrpcAddressLastSeen = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
					ProfileJson = table.Column<string>(type: "text", nullable: false),
					Hostname = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
					OsDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
					CpuLogicalCores = table.Column<int>(type: "integer", nullable: true),
					GpuPrimaryName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
					GpuVramBytes = table.Column<long>(type: "bigint", nullable: true),
					RamTotalBytes = table.Column<long>(type: "bigint", nullable: true),
					UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_AiWorkerHostProfiles", x => x.Id);
				});

			migrationBuilder.CreateTable(
				name: "AiWorkerHostRefreshMetas",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					LastRefreshAttemptUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
					LastRefreshSucceeded = table.Column<bool>(type: "boolean", nullable: false),
					LastRefreshError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
					GrpcAddressConfigured = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_AiWorkerHostRefreshMetas", x => x.Id);
				});

			migrationBuilder.CreateIndex(
				name: "IX_AiWorkerHostProfiles_UpdatedAtUtc",
				table: "AiWorkerHostProfiles",
				column: "UpdatedAtUtc");

			migrationBuilder.CreateIndex(
				name: "IX_AiWorkerHostProfiles_WorkerInstanceId",
				table: "AiWorkerHostProfiles",
				column: "WorkerInstanceId",
				unique: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "AiWorkerHostProfiles");

			migrationBuilder.DropTable(
				name: "AiWorkerHostRefreshMetas");
		}
	}
}
