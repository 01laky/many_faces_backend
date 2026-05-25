using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddOperatorPushSystemSettings : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "OperatorPushSystemSettings",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					Enabled = table.Column<bool>(type: "boolean", nullable: false),
					WorkerGrpcUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
					WorkerAuthTokenCiphertext = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
					FirebaseProjectId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
					FirebaseServiceAccountJsonCiphertext = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: true),
					DefaultTitleLocKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
					DefaultBodyLocKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
					DefaultAndroidChannelId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
					GrpcDeadlineSeconds = table.Column<int>(type: "integer", nullable: false),
					UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					UpdatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_OperatorPushSystemSettings", x => x.Id);
				});
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "OperatorPushSystemSettings");
		}
	}
}
