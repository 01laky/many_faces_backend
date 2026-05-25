using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddFaceVideoLounges : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<bool>(
				name: "VideoLoungesCreate",
				table: "Faces",
				type: "boolean",
				nullable: false,
				defaultValue: false);

			migrationBuilder.CreateTable(
				name: "FaceVideoLounges",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					FaceId = table.Column<int>(type: "integer", nullable: false),
					Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
					Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
					IsPublic = table.Column<bool>(type: "boolean", nullable: false),
					IsSystemManaged = table.Column<bool>(type: "boolean", nullable: false),
					CreatorUserId = table.Column<string>(type: "text", nullable: true),
					MaxParticipants = table.Column<int>(type: "integer", nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_FaceVideoLounges", x => x.Id);
					table.ForeignKey(
						name: "FK_FaceVideoLounges_AspNetUsers_CreatorUserId",
						column: x => x.CreatorUserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.SetNull);
					table.ForeignKey(
						name: "FK_FaceVideoLounges_Faces_FaceId",
						column: x => x.FaceId,
						principalTable: "Faces",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "FaceVideoLoungeJoinRequests",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					FaceVideoLoungeId = table.Column<int>(type: "integer", nullable: false),
					UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					Status = table.Column<int>(type: "integer", nullable: false),
					RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_FaceVideoLoungeJoinRequests", x => x.Id);
					table.ForeignKey(
						name: "FK_FaceVideoLoungeJoinRequests_AspNetUsers_UserId",
						column: x => x.UserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_FaceVideoLoungeJoinRequests_FaceVideoLounges_FaceVideoLoung~",
						column: x => x.FaceVideoLoungeId,
						principalTable: "FaceVideoLounges",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "FaceVideoLoungeMembers",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					FaceVideoLoungeId = table.Column<int>(type: "integer", nullable: false),
					UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_FaceVideoLoungeMembers", x => x.Id);
					table.ForeignKey(
						name: "FK_FaceVideoLoungeMembers_AspNetUsers_UserId",
						column: x => x.UserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_FaceVideoLoungeMembers_FaceVideoLounges_FaceVideoLoungeId",
						column: x => x.FaceVideoLoungeId,
						principalTable: "FaceVideoLounges",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "FaceVideoLoungeSessions",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					FaceVideoLoungeId = table.Column<int>(type: "integer", nullable: false),
					StartedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
					LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_FaceVideoLoungeSessions", x => x.Id);
					table.ForeignKey(
						name: "FK_FaceVideoLoungeSessions_FaceVideoLounges_FaceVideoLoungeId",
						column: x => x.FaceVideoLoungeId,
						principalTable: "FaceVideoLounges",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "FaceVideoLoungeSessionParticipants",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					FaceVideoLoungeSessionId = table.Column<int>(type: "integer", nullable: false),
					UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					JoinMode = table.Column<int>(type: "integer", nullable: false),
					AudioEnabled = table.Column<bool>(type: "boolean", nullable: false),
					VideoEnabled = table.Column<bool>(type: "boolean", nullable: false),
					JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					IsListedInPublicRoster = table.Column<bool>(type: "boolean", nullable: false),
					LeftAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_FaceVideoLoungeSessionParticipants", x => x.Id);
					table.ForeignKey(
						name: "FK_FaceVideoLoungeSessionParticipants_AspNetUsers_UserId",
						column: x => x.UserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_FaceVideoLoungeSessionParticipants_FaceVideoLoungeSessions_~",
						column: x => x.FaceVideoLoungeSessionId,
						principalTable: "FaceVideoLoungeSessions",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "IX_FaceVideoLoungeJoinRequests_FaceVideoLoungeId_UserId_Status",
				table: "FaceVideoLoungeJoinRequests",
				columns: new[] { "FaceVideoLoungeId", "UserId", "Status" });

			migrationBuilder.CreateIndex(
				name: "IX_FaceVideoLoungeJoinRequests_UserId",
				table: "FaceVideoLoungeJoinRequests",
				column: "UserId");

			migrationBuilder.CreateIndex(
				name: "IX_FaceVideoLoungeMembers_FaceVideoLoungeId_UserId",
				table: "FaceVideoLoungeMembers",
				columns: new[] { "FaceVideoLoungeId", "UserId" },
				unique: true);

			migrationBuilder.CreateIndex(
				name: "IX_FaceVideoLoungeMembers_UserId",
				table: "FaceVideoLoungeMembers",
				column: "UserId");

			migrationBuilder.CreateIndex(
				name: "IX_FaceVideoLounges_CreatorUserId",
				table: "FaceVideoLounges",
				column: "CreatorUserId");

			migrationBuilder.CreateIndex(
				name: "IX_FaceVideoLounges_FaceId",
				table: "FaceVideoLounges",
				column: "FaceId");

			migrationBuilder.CreateIndex(
				name: "IX_FaceVideoLoungeSessionParticipants_FaceVideoLoungeSessionId~",
				table: "FaceVideoLoungeSessionParticipants",
				columns: new[] { "FaceVideoLoungeSessionId", "UserId", "LeftAt" });

			migrationBuilder.CreateIndex(
				name: "IX_FaceVideoLoungeSessionParticipants_UserId",
				table: "FaceVideoLoungeSessionParticipants",
				column: "UserId");

			migrationBuilder.CreateIndex(
				name: "IX_FaceVideoLoungeSessions_FaceVideoLoungeId",
				table: "FaceVideoLoungeSessions",
				column: "FaceVideoLoungeId");

			migrationBuilder.CreateIndex(
				name: "IX_FaceVideoLoungeSessions_FaceVideoLoungeId_EndedAt",
				table: "FaceVideoLoungeSessions",
				columns: new[] { "FaceVideoLoungeId", "EndedAt" });
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "FaceVideoLoungeJoinRequests");

			migrationBuilder.DropTable(
				name: "FaceVideoLoungeMembers");

			migrationBuilder.DropTable(
				name: "FaceVideoLoungeSessionParticipants");

			migrationBuilder.DropTable(
				name: "FaceVideoLoungeSessions");

			migrationBuilder.DropTable(
				name: "FaceVideoLounges");

			migrationBuilder.DropColumn(
				name: "VideoLoungesCreate",
				table: "Faces");
		}
	}
}
