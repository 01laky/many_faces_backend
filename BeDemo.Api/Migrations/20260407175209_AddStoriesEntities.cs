using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddStoriesEntities : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "Stories",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					CreatorId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
					State = table.Column<int>(type: "integer", nullable: false),
					PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
					ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
					ScheduledPublishAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_Stories", x => x.Id);
					table.ForeignKey(
						name: "FK_Stories_AspNetUsers_CreatorId",
						column: x => x.CreatorId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "StoryComments",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					StoryId = table.Column<int>(type: "integer", nullable: false),
					UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_StoryComments", x => x.Id);
					table.ForeignKey(
						name: "FK_StoryComments_AspNetUsers_UserId",
						column: x => x.UserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_StoryComments_Stories_StoryId",
						column: x => x.StoryId,
						principalTable: "Stories",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "StoryFaces",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					StoryId = table.Column<int>(type: "integer", nullable: false),
					FaceId = table.Column<int>(type: "integer", nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_StoryFaces", x => x.Id);
					table.ForeignKey(
						name: "FK_StoryFaces_Faces_FaceId",
						column: x => x.FaceId,
						principalTable: "Faces",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict);
					table.ForeignKey(
						name: "FK_StoryFaces_Stories_StoryId",
						column: x => x.StoryId,
						principalTable: "Stories",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "StoryImages",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					StoryId = table.Column<int>(type: "integer", nullable: false),
					ImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
					Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
					SortOrder = table.Column<int>(type: "integer", nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_StoryImages", x => x.Id);
					table.ForeignKey(
						name: "FK_StoryImages_Stories_StoryId",
						column: x => x.StoryId,
						principalTable: "Stories",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "StoryLikes",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					StoryId = table.Column<int>(type: "integer", nullable: false),
					UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_StoryLikes", x => x.Id);
					table.ForeignKey(
						name: "FK_StoryLikes_AspNetUsers_UserId",
						column: x => x.UserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_StoryLikes_Stories_StoryId",
						column: x => x.StoryId,
						principalTable: "Stories",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "StoryViews",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					StoryId = table.Column<int>(type: "integer", nullable: false),
					ViewerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					ViewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_StoryViews", x => x.Id);
					table.ForeignKey(
						name: "FK_StoryViews_AspNetUsers_ViewerUserId",
						column: x => x.ViewerUserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_StoryViews_Stories_StoryId",
						column: x => x.StoryId,
						principalTable: "Stories",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "IX_Stories_CreatorId",
				table: "Stories",
				column: "CreatorId");

			migrationBuilder.CreateIndex(
				name: "IX_Stories_State_PublishedAt_ExpiresAt",
				table: "Stories",
				columns: new[] { "State", "PublishedAt", "ExpiresAt" });

			migrationBuilder.CreateIndex(
				name: "IX_StoryComments_StoryId",
				table: "StoryComments",
				column: "StoryId");

			migrationBuilder.CreateIndex(
				name: "IX_StoryComments_UserId",
				table: "StoryComments",
				column: "UserId");

			migrationBuilder.CreateIndex(
				name: "IX_StoryFaces_FaceId",
				table: "StoryFaces",
				column: "FaceId");

			migrationBuilder.CreateIndex(
				name: "IX_StoryFaces_StoryId_FaceId",
				table: "StoryFaces",
				columns: new[] { "StoryId", "FaceId" },
				unique: true);

			migrationBuilder.CreateIndex(
				name: "IX_StoryImages_StoryId_SortOrder",
				table: "StoryImages",
				columns: new[] { "StoryId", "SortOrder" },
				unique: true);

			migrationBuilder.CreateIndex(
				name: "IX_StoryLikes_StoryId_UserId",
				table: "StoryLikes",
				columns: new[] { "StoryId", "UserId" },
				unique: true);

			migrationBuilder.CreateIndex(
				name: "IX_StoryLikes_UserId",
				table: "StoryLikes",
				column: "UserId");

			migrationBuilder.CreateIndex(
				name: "IX_StoryViews_StoryId_ViewerUserId",
				table: "StoryViews",
				columns: new[] { "StoryId", "ViewerUserId" },
				unique: true);

			migrationBuilder.CreateIndex(
				name: "IX_StoryViews_ViewerUserId",
				table: "StoryViews",
				column: "ViewerUserId");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "StoryComments");

			migrationBuilder.DropTable(
				name: "StoryFaces");

			migrationBuilder.DropTable(
				name: "StoryImages");

			migrationBuilder.DropTable(
				name: "StoryLikes");

			migrationBuilder.DropTable(
				name: "StoryViews");

			migrationBuilder.DropTable(
				name: "Stories");
		}
	}
}
