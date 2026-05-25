using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddUserContentModerationWorkflow : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			AddModerationColumns(migrationBuilder, "Albums");
			AddModerationColumns(migrationBuilder, "Blogs");
			AddModerationColumns(migrationBuilder, "Reels");

			migrationBuilder.CreateTable(
				name: "AiReviewJobs",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					ContentType = table.Column<int>(type: "integer", nullable: false),
					ContentId = table.Column<int>(type: "integer", nullable: false),
					FaceId = table.Column<int>(type: "integer", nullable: false),
					CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					Priority = table.Column<int>(type: "integer", nullable: false),
					Status = table.Column<int>(type: "integer", nullable: false),
					Attempts = table.Column<int>(type: "integer", nullable: false),
					MaxAttempts = table.Column<int>(type: "integer", nullable: false),
					ModerationVersion = table.Column<int>(type: "integer", nullable: false),
					NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
					CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
					CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
					LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
				},
				constraints: table => table.PrimaryKey("PK_AiReviewJobs", x => x.Id));

			migrationBuilder.CreateTable(
				name: "ContentModerationEvents",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					ContentType = table.Column<int>(type: "integer", nullable: false),
					ContentId = table.Column<int>(type: "integer", nullable: false),
					FaceId = table.Column<int>(type: "integer", nullable: false),
					OldApprovalStatus = table.Column<int>(type: "integer", nullable: true),
					NewApprovalStatus = table.Column<int>(type: "integer", nullable: true),
					OldAiReviewStatus = table.Column<int>(type: "integer", nullable: true),
					NewAiReviewStatus = table.Column<int>(type: "integer", nullable: true),
					ActorType = table.Column<int>(type: "integer", nullable: false),
					ActorUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
					Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
					UserMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
					AiTraceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
					AiModelVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
					CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table => table.PrimaryKey("PK_ContentModerationEvents", x => x.Id));

			migrationBuilder.CreateIndex(name: "IX_Albums_ApprovalStatus", table: "Albums", column: "ApprovalStatus");
			migrationBuilder.CreateIndex(name: "IX_Albums_AiReviewStatus", table: "Albums", column: "AiReviewStatus");
			migrationBuilder.CreateIndex(name: "IX_Blogs_ApprovalStatus", table: "Blogs", column: "ApprovalStatus");
			migrationBuilder.CreateIndex(name: "IX_Blogs_AiReviewStatus", table: "Blogs", column: "AiReviewStatus");
			migrationBuilder.CreateIndex(name: "IX_Reels_ApprovalStatus", table: "Reels", column: "ApprovalStatus");
			migrationBuilder.CreateIndex(name: "IX_Reels_AiReviewStatus", table: "Reels", column: "AiReviewStatus");
			migrationBuilder.CreateIndex(
				name: "IX_AiReviewJobs_ContentType_ContentId_ModerationVersion",
				table: "AiReviewJobs",
				columns: new[] { "ContentType", "ContentId", "ModerationVersion" },
				unique: true);
			migrationBuilder.CreateIndex(
				name: "IX_AiReviewJobs_Status_NextAttemptAtUtc",
				table: "AiReviewJobs",
				columns: new[] { "Status", "NextAttemptAtUtc" });
			migrationBuilder.CreateIndex(name: "IX_AiReviewJobs_FaceId", table: "AiReviewJobs", column: "FaceId");
			migrationBuilder.CreateIndex(
				name: "IX_ContentModerationEvents_ContentType_ContentId_CreatedAtUtc",
				table: "ContentModerationEvents",
				columns: new[] { "ContentType", "ContentId", "CreatedAtUtc" });
			migrationBuilder.CreateIndex(name: "IX_ContentModerationEvents_FaceId", table: "ContentModerationEvents", column: "FaceId");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(name: "AiReviewJobs");
			migrationBuilder.DropTable(name: "ContentModerationEvents");
			DropModerationColumns(migrationBuilder, "Albums");
			DropModerationColumns(migrationBuilder, "Blogs");
			DropModerationColumns(migrationBuilder, "Reels");
		}

		private static void AddModerationColumns(MigrationBuilder migrationBuilder, string table)
		{
			// Existing content remains approved; create endpoints set PendingApproval explicitly.
			migrationBuilder.AddColumn<int>(name: "ApprovalStatus", table: table, type: "integer", nullable: false, defaultValue: 2);
			migrationBuilder.AddColumn<int>(name: "AiReviewStatus", table: table, type: "integer", nullable: false, defaultValue: 1);
			migrationBuilder.AddColumn<int>(name: "AiReviewDecision", table: table, type: "integer", nullable: false, defaultValue: 0);
			migrationBuilder.AddColumn<double>(name: "AiReviewConfidence", table: table, type: "double precision", nullable: true);
			migrationBuilder.AddColumn<int>(name: "AiReviewRiskLevel", table: table, type: "integer", nullable: false, defaultValue: 0);
			migrationBuilder.AddColumn<string>(name: "AiReviewFlagsJson", table: table, type: "text", nullable: true);
			migrationBuilder.AddColumn<string>(name: "AiReviewReason", table: table, type: "character varying(2000)", maxLength: 2000, nullable: true);
			migrationBuilder.AddColumn<string>(name: "AiReviewUserMessage", table: table, type: "character varying(1000)", maxLength: 1000, nullable: true);
			migrationBuilder.AddColumn<string>(name: "AiReviewModelVersion", table: table, type: "character varying(100)", maxLength: 100, nullable: true);
			migrationBuilder.AddColumn<string>(name: "AiReviewTraceId", table: table, type: "character varying(200)", maxLength: 200, nullable: true);
			migrationBuilder.AddColumn<DateTime>(name: "SubmittedAtUtc", table: table, type: "timestamp with time zone", nullable: true);
			migrationBuilder.AddColumn<DateTime>(name: "AiReviewedAtUtc", table: table, type: "timestamp with time zone", nullable: true);
			migrationBuilder.AddColumn<DateTime>(name: "HumanReviewedAtUtc", table: table, type: "timestamp with time zone", nullable: true);
			migrationBuilder.AddColumn<string>(name: "HumanReviewedByUserId", table: table, type: "character varying(450)", maxLength: 450, nullable: true);
			migrationBuilder.AddColumn<string>(name: "HumanDecisionReason", table: table, type: "character varying(2000)", maxLength: 2000, nullable: true);
			migrationBuilder.AddColumn<DateTime>(name: "RemovedAtUtc", table: table, type: "timestamp with time zone", nullable: true);
			migrationBuilder.AddColumn<string>(name: "RemovedByUserId", table: table, type: "character varying(450)", maxLength: 450, nullable: true);
			migrationBuilder.AddColumn<string>(name: "RemovalReason", table: table, type: "character varying(2000)", maxLength: 2000, nullable: true);
			migrationBuilder.AddColumn<int>(name: "ModerationVersion", table: table, type: "integer", nullable: false, defaultValue: 1);
		}

		private static void DropModerationColumns(MigrationBuilder migrationBuilder, string table)
		{
			migrationBuilder.DropIndex(name: $"IX_{table}_ApprovalStatus", table: table);
			migrationBuilder.DropIndex(name: $"IX_{table}_AiReviewStatus", table: table);
			migrationBuilder.DropColumn(name: "ApprovalStatus", table: table);
			migrationBuilder.DropColumn(name: "AiReviewStatus", table: table);
			migrationBuilder.DropColumn(name: "AiReviewDecision", table: table);
			migrationBuilder.DropColumn(name: "AiReviewConfidence", table: table);
			migrationBuilder.DropColumn(name: "AiReviewRiskLevel", table: table);
			migrationBuilder.DropColumn(name: "AiReviewFlagsJson", table: table);
			migrationBuilder.DropColumn(name: "AiReviewReason", table: table);
			migrationBuilder.DropColumn(name: "AiReviewUserMessage", table: table);
			migrationBuilder.DropColumn(name: "AiReviewModelVersion", table: table);
			migrationBuilder.DropColumn(name: "AiReviewTraceId", table: table);
			migrationBuilder.DropColumn(name: "SubmittedAtUtc", table: table);
			migrationBuilder.DropColumn(name: "AiReviewedAtUtc", table: table);
			migrationBuilder.DropColumn(name: "HumanReviewedAtUtc", table: table);
			migrationBuilder.DropColumn(name: "HumanReviewedByUserId", table: table);
			migrationBuilder.DropColumn(name: "HumanDecisionReason", table: table);
			migrationBuilder.DropColumn(name: "RemovedAtUtc", table: table);
			migrationBuilder.DropColumn(name: "RemovedByUserId", table: table);
			migrationBuilder.DropColumn(name: "RemovalReason", table: table);
			migrationBuilder.DropColumn(name: "ModerationVersion", table: table);
		}
	}
}
