using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class SyncPendingModelState : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<double>(
				name: "AiReviewConfidence",
				table: "Reels",
				type: "double precision",
				nullable: true);

			migrationBuilder.AddColumn<int>(
				name: "AiReviewDecision",
				table: "Reels",
				type: "integer",
				nullable: false,
				defaultValue: 0);

			migrationBuilder.AddColumn<string>(
				name: "AiReviewFlagsJson",
				table: "Reels",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "AiReviewModelVersion",
				table: "Reels",
				type: "character varying(100)",
				maxLength: 100,
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "AiReviewReason",
				table: "Reels",
				type: "character varying(2000)",
				maxLength: 2000,
				nullable: true);

			migrationBuilder.AddColumn<int>(
				name: "AiReviewRiskLevel",
				table: "Reels",
				type: "integer",
				nullable: false,
				defaultValue: 0);

			migrationBuilder.AddColumn<int>(
				name: "AiReviewStatus",
				table: "Reels",
				type: "integer",
				nullable: false,
				defaultValue: 0);

			migrationBuilder.AddColumn<string>(
				name: "AiReviewTraceId",
				table: "Reels",
				type: "character varying(200)",
				maxLength: 200,
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "AiReviewUserMessage",
				table: "Reels",
				type: "character varying(1000)",
				maxLength: 1000,
				nullable: true);

			migrationBuilder.AddColumn<DateTime>(
				name: "AiReviewedAtUtc",
				table: "Reels",
				type: "timestamp with time zone",
				nullable: true);

			migrationBuilder.AddColumn<int>(
				name: "ApprovalStatus",
				table: "Reels",
				type: "integer",
				nullable: false,
				defaultValue: 0);

			migrationBuilder.AddColumn<string>(
				name: "HumanDecisionReason",
				table: "Reels",
				type: "character varying(2000)",
				maxLength: 2000,
				nullable: true);

			migrationBuilder.AddColumn<DateTime>(
				name: "HumanReviewedAtUtc",
				table: "Reels",
				type: "timestamp with time zone",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "HumanReviewedByUserId",
				table: "Reels",
				type: "character varying(450)",
				maxLength: 450,
				nullable: true);

			migrationBuilder.AddColumn<int>(
				name: "ModerationVersion",
				table: "Reels",
				type: "integer",
				nullable: false,
				defaultValue: 0);

			migrationBuilder.AddColumn<string>(
				name: "RemovalReason",
				table: "Reels",
				type: "character varying(2000)",
				maxLength: 2000,
				nullable: true);

			migrationBuilder.AddColumn<DateTime>(
				name: "RemovedAtUtc",
				table: "Reels",
				type: "timestamp with time zone",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "RemovedByUserId",
				table: "Reels",
				type: "character varying(450)",
				maxLength: 450,
				nullable: true);

			migrationBuilder.AddColumn<DateTime>(
				name: "SubmittedAtUtc",
				table: "Reels",
				type: "timestamp with time zone",
				nullable: true);

			migrationBuilder.AddColumn<double>(
				name: "AiReviewConfidence",
				table: "Blogs",
				type: "double precision",
				nullable: true);

			migrationBuilder.AddColumn<int>(
				name: "AiReviewDecision",
				table: "Blogs",
				type: "integer",
				nullable: false,
				defaultValue: 0);

			migrationBuilder.AddColumn<string>(
				name: "AiReviewFlagsJson",
				table: "Blogs",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "AiReviewModelVersion",
				table: "Blogs",
				type: "character varying(100)",
				maxLength: 100,
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "AiReviewReason",
				table: "Blogs",
				type: "character varying(2000)",
				maxLength: 2000,
				nullable: true);

			migrationBuilder.AddColumn<int>(
				name: "AiReviewRiskLevel",
				table: "Blogs",
				type: "integer",
				nullable: false,
				defaultValue: 0);

			migrationBuilder.AddColumn<int>(
				name: "AiReviewStatus",
				table: "Blogs",
				type: "integer",
				nullable: false,
				defaultValue: 0);

			migrationBuilder.AddColumn<string>(
				name: "AiReviewTraceId",
				table: "Blogs",
				type: "character varying(200)",
				maxLength: 200,
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "AiReviewUserMessage",
				table: "Blogs",
				type: "character varying(1000)",
				maxLength: 1000,
				nullable: true);

			migrationBuilder.AddColumn<DateTime>(
				name: "AiReviewedAtUtc",
				table: "Blogs",
				type: "timestamp with time zone",
				nullable: true);

			migrationBuilder.AddColumn<int>(
				name: "ApprovalStatus",
				table: "Blogs",
				type: "integer",
				nullable: false,
				defaultValue: 0);

			migrationBuilder.AddColumn<string>(
				name: "HumanDecisionReason",
				table: "Blogs",
				type: "character varying(2000)",
				maxLength: 2000,
				nullable: true);

			migrationBuilder.AddColumn<DateTime>(
				name: "HumanReviewedAtUtc",
				table: "Blogs",
				type: "timestamp with time zone",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "HumanReviewedByUserId",
				table: "Blogs",
				type: "character varying(450)",
				maxLength: 450,
				nullable: true);

			migrationBuilder.AddColumn<int>(
				name: "ModerationVersion",
				table: "Blogs",
				type: "integer",
				nullable: false,
				defaultValue: 0);

			migrationBuilder.AddColumn<string>(
				name: "RemovalReason",
				table: "Blogs",
				type: "character varying(2000)",
				maxLength: 2000,
				nullable: true);

			migrationBuilder.AddColumn<DateTime>(
				name: "RemovedAtUtc",
				table: "Blogs",
				type: "timestamp with time zone",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "RemovedByUserId",
				table: "Blogs",
				type: "character varying(450)",
				maxLength: 450,
				nullable: true);

			migrationBuilder.AddColumn<DateTime>(
				name: "SubmittedAtUtc",
				table: "Blogs",
				type: "timestamp with time zone",
				nullable: true);

			migrationBuilder.AddColumn<double>(
				name: "AiReviewConfidence",
				table: "Albums",
				type: "double precision",
				nullable: true);

			migrationBuilder.AddColumn<int>(
				name: "AiReviewDecision",
				table: "Albums",
				type: "integer",
				nullable: false,
				defaultValue: 0);

			migrationBuilder.AddColumn<string>(
				name: "AiReviewFlagsJson",
				table: "Albums",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "AiReviewModelVersion",
				table: "Albums",
				type: "character varying(100)",
				maxLength: 100,
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "AiReviewReason",
				table: "Albums",
				type: "character varying(2000)",
				maxLength: 2000,
				nullable: true);

			migrationBuilder.AddColumn<int>(
				name: "AiReviewRiskLevel",
				table: "Albums",
				type: "integer",
				nullable: false,
				defaultValue: 0);

			migrationBuilder.AddColumn<int>(
				name: "AiReviewStatus",
				table: "Albums",
				type: "integer",
				nullable: false,
				defaultValue: 0);

			migrationBuilder.AddColumn<string>(
				name: "AiReviewTraceId",
				table: "Albums",
				type: "character varying(200)",
				maxLength: 200,
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "AiReviewUserMessage",
				table: "Albums",
				type: "character varying(1000)",
				maxLength: 1000,
				nullable: true);

			migrationBuilder.AddColumn<DateTime>(
				name: "AiReviewedAtUtc",
				table: "Albums",
				type: "timestamp with time zone",
				nullable: true);

			migrationBuilder.AddColumn<int>(
				name: "ApprovalStatus",
				table: "Albums",
				type: "integer",
				nullable: false,
				defaultValue: 0);

			migrationBuilder.AddColumn<string>(
				name: "HumanDecisionReason",
				table: "Albums",
				type: "character varying(2000)",
				maxLength: 2000,
				nullable: true);

			migrationBuilder.AddColumn<DateTime>(
				name: "HumanReviewedAtUtc",
				table: "Albums",
				type: "timestamp with time zone",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "HumanReviewedByUserId",
				table: "Albums",
				type: "character varying(450)",
				maxLength: 450,
				nullable: true);

			migrationBuilder.AddColumn<int>(
				name: "ModerationVersion",
				table: "Albums",
				type: "integer",
				nullable: false,
				defaultValue: 0);

			migrationBuilder.AddColumn<string>(
				name: "RemovalReason",
				table: "Albums",
				type: "character varying(2000)",
				maxLength: 2000,
				nullable: true);

			migrationBuilder.AddColumn<DateTime>(
				name: "RemovedAtUtc",
				table: "Albums",
				type: "timestamp with time zone",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "RemovedByUserId",
				table: "Albums",
				type: "character varying(450)",
				maxLength: 450,
				nullable: true);

			migrationBuilder.AddColumn<DateTime>(
				name: "SubmittedAtUtc",
				table: "Albums",
				type: "timestamp with time zone",
				nullable: true);

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
				constraints: table =>
				{
					table.PrimaryKey("PK_AiReviewJobs", x => x.Id);
				});

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
				constraints: table =>
				{
					table.PrimaryKey("PK_ContentModerationEvents", x => x.Id);
				});

			migrationBuilder.CreateIndex(
				name: "IX_Reels_AiReviewStatus",
				table: "Reels",
				column: "AiReviewStatus");

			migrationBuilder.CreateIndex(
				name: "IX_Reels_ApprovalStatus",
				table: "Reels",
				column: "ApprovalStatus");

			migrationBuilder.CreateIndex(
				name: "IX_Blogs_AiReviewStatus",
				table: "Blogs",
				column: "AiReviewStatus");

			migrationBuilder.CreateIndex(
				name: "IX_Blogs_ApprovalStatus",
				table: "Blogs",
				column: "ApprovalStatus");

			migrationBuilder.CreateIndex(
				name: "IX_Albums_AiReviewStatus",
				table: "Albums",
				column: "AiReviewStatus");

			migrationBuilder.CreateIndex(
				name: "IX_Albums_ApprovalStatus",
				table: "Albums",
				column: "ApprovalStatus");

			migrationBuilder.CreateIndex(
				name: "IX_AiReviewJobs_ContentType_ContentId_ModerationVersion",
				table: "AiReviewJobs",
				columns: new[] { "ContentType", "ContentId", "ModerationVersion" },
				unique: true);

			migrationBuilder.CreateIndex(
				name: "IX_AiReviewJobs_FaceId",
				table: "AiReviewJobs",
				column: "FaceId");

			migrationBuilder.CreateIndex(
				name: "IX_AiReviewJobs_Status_NextAttemptAtUtc",
				table: "AiReviewJobs",
				columns: new[] { "Status", "NextAttemptAtUtc" });

			migrationBuilder.CreateIndex(
				name: "IX_ContentModerationEvents_ContentType_ContentId_CreatedAtUtc",
				table: "ContentModerationEvents",
				columns: new[] { "ContentType", "ContentId", "CreatedAtUtc" });

			migrationBuilder.CreateIndex(
				name: "IX_ContentModerationEvents_FaceId",
				table: "ContentModerationEvents",
				column: "FaceId");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "AiReviewJobs");

			migrationBuilder.DropTable(
				name: "ContentModerationEvents");

			migrationBuilder.DropIndex(
				name: "IX_Reels_AiReviewStatus",
				table: "Reels");

			migrationBuilder.DropIndex(
				name: "IX_Reels_ApprovalStatus",
				table: "Reels");

			migrationBuilder.DropIndex(
				name: "IX_Blogs_AiReviewStatus",
				table: "Blogs");

			migrationBuilder.DropIndex(
				name: "IX_Blogs_ApprovalStatus",
				table: "Blogs");

			migrationBuilder.DropIndex(
				name: "IX_Albums_AiReviewStatus",
				table: "Albums");

			migrationBuilder.DropIndex(
				name: "IX_Albums_ApprovalStatus",
				table: "Albums");

			migrationBuilder.DropColumn(
				name: "AiReviewConfidence",
				table: "Reels");

			migrationBuilder.DropColumn(
				name: "AiReviewDecision",
				table: "Reels");

			migrationBuilder.DropColumn(
				name: "AiReviewFlagsJson",
				table: "Reels");

			migrationBuilder.DropColumn(
				name: "AiReviewModelVersion",
				table: "Reels");

			migrationBuilder.DropColumn(
				name: "AiReviewReason",
				table: "Reels");

			migrationBuilder.DropColumn(
				name: "AiReviewRiskLevel",
				table: "Reels");

			migrationBuilder.DropColumn(
				name: "AiReviewStatus",
				table: "Reels");

			migrationBuilder.DropColumn(
				name: "AiReviewTraceId",
				table: "Reels");

			migrationBuilder.DropColumn(
				name: "AiReviewUserMessage",
				table: "Reels");

			migrationBuilder.DropColumn(
				name: "AiReviewedAtUtc",
				table: "Reels");

			migrationBuilder.DropColumn(
				name: "ApprovalStatus",
				table: "Reels");

			migrationBuilder.DropColumn(
				name: "HumanDecisionReason",
				table: "Reels");

			migrationBuilder.DropColumn(
				name: "HumanReviewedAtUtc",
				table: "Reels");

			migrationBuilder.DropColumn(
				name: "HumanReviewedByUserId",
				table: "Reels");

			migrationBuilder.DropColumn(
				name: "ModerationVersion",
				table: "Reels");

			migrationBuilder.DropColumn(
				name: "RemovalReason",
				table: "Reels");

			migrationBuilder.DropColumn(
				name: "RemovedAtUtc",
				table: "Reels");

			migrationBuilder.DropColumn(
				name: "RemovedByUserId",
				table: "Reels");

			migrationBuilder.DropColumn(
				name: "SubmittedAtUtc",
				table: "Reels");

			migrationBuilder.DropColumn(
				name: "AiReviewConfidence",
				table: "Blogs");

			migrationBuilder.DropColumn(
				name: "AiReviewDecision",
				table: "Blogs");

			migrationBuilder.DropColumn(
				name: "AiReviewFlagsJson",
				table: "Blogs");

			migrationBuilder.DropColumn(
				name: "AiReviewModelVersion",
				table: "Blogs");

			migrationBuilder.DropColumn(
				name: "AiReviewReason",
				table: "Blogs");

			migrationBuilder.DropColumn(
				name: "AiReviewRiskLevel",
				table: "Blogs");

			migrationBuilder.DropColumn(
				name: "AiReviewStatus",
				table: "Blogs");

			migrationBuilder.DropColumn(
				name: "AiReviewTraceId",
				table: "Blogs");

			migrationBuilder.DropColumn(
				name: "AiReviewUserMessage",
				table: "Blogs");

			migrationBuilder.DropColumn(
				name: "AiReviewedAtUtc",
				table: "Blogs");

			migrationBuilder.DropColumn(
				name: "ApprovalStatus",
				table: "Blogs");

			migrationBuilder.DropColumn(
				name: "HumanDecisionReason",
				table: "Blogs");

			migrationBuilder.DropColumn(
				name: "HumanReviewedAtUtc",
				table: "Blogs");

			migrationBuilder.DropColumn(
				name: "HumanReviewedByUserId",
				table: "Blogs");

			migrationBuilder.DropColumn(
				name: "ModerationVersion",
				table: "Blogs");

			migrationBuilder.DropColumn(
				name: "RemovalReason",
				table: "Blogs");

			migrationBuilder.DropColumn(
				name: "RemovedAtUtc",
				table: "Blogs");

			migrationBuilder.DropColumn(
				name: "RemovedByUserId",
				table: "Blogs");

			migrationBuilder.DropColumn(
				name: "SubmittedAtUtc",
				table: "Blogs");

			migrationBuilder.DropColumn(
				name: "AiReviewConfidence",
				table: "Albums");

			migrationBuilder.DropColumn(
				name: "AiReviewDecision",
				table: "Albums");

			migrationBuilder.DropColumn(
				name: "AiReviewFlagsJson",
				table: "Albums");

			migrationBuilder.DropColumn(
				name: "AiReviewModelVersion",
				table: "Albums");

			migrationBuilder.DropColumn(
				name: "AiReviewReason",
				table: "Albums");

			migrationBuilder.DropColumn(
				name: "AiReviewRiskLevel",
				table: "Albums");

			migrationBuilder.DropColumn(
				name: "AiReviewStatus",
				table: "Albums");

			migrationBuilder.DropColumn(
				name: "AiReviewTraceId",
				table: "Albums");

			migrationBuilder.DropColumn(
				name: "AiReviewUserMessage",
				table: "Albums");

			migrationBuilder.DropColumn(
				name: "AiReviewedAtUtc",
				table: "Albums");

			migrationBuilder.DropColumn(
				name: "ApprovalStatus",
				table: "Albums");

			migrationBuilder.DropColumn(
				name: "HumanDecisionReason",
				table: "Albums");

			migrationBuilder.DropColumn(
				name: "HumanReviewedAtUtc",
				table: "Albums");

			migrationBuilder.DropColumn(
				name: "HumanReviewedByUserId",
				table: "Albums");

			migrationBuilder.DropColumn(
				name: "ModerationVersion",
				table: "Albums");

			migrationBuilder.DropColumn(
				name: "RemovalReason",
				table: "Albums");

			migrationBuilder.DropColumn(
				name: "RemovedAtUtc",
				table: "Albums");

			migrationBuilder.DropColumn(
				name: "RemovedByUserId",
				table: "Albums");

			migrationBuilder.DropColumn(
				name: "SubmittedAtUtc",
				table: "Albums");
		}
	}
}
