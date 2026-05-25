using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddReelEntities : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "Reels",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					CreatorId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
					Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
					VideoUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_Reels", x => x.Id);
					table.ForeignKey(
						name: "FK_Reels_AspNetUsers_CreatorId",
						column: x => x.CreatorId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "ReelComments",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					ReelId = table.Column<int>(type: "integer", nullable: false),
					UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_ReelComments", x => x.Id);
					table.ForeignKey(
						name: "FK_ReelComments_AspNetUsers_UserId",
						column: x => x.UserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_ReelComments_Reels_ReelId",
						column: x => x.ReelId,
						principalTable: "Reels",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "ReelFaces",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					ReelId = table.Column<int>(type: "integer", nullable: false),
					FaceId = table.Column<int>(type: "integer", nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_ReelFaces", x => x.Id);
					table.ForeignKey(
						name: "FK_ReelFaces_Faces_FaceId",
						column: x => x.FaceId,
						principalTable: "Faces",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict);
					table.ForeignKey(
						name: "FK_ReelFaces_Reels_ReelId",
						column: x => x.ReelId,
						principalTable: "Reels",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "ReelLikes",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					ReelId = table.Column<int>(type: "integer", nullable: false),
					UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_ReelLikes", x => x.Id);
					table.ForeignKey(
						name: "FK_ReelLikes_AspNetUsers_UserId",
						column: x => x.UserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_ReelLikes_Reels_ReelId",
						column: x => x.ReelId,
						principalTable: "Reels",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "IX_ReelComments_ReelId",
				table: "ReelComments",
				column: "ReelId");

			migrationBuilder.CreateIndex(
				name: "IX_ReelComments_UserId",
				table: "ReelComments",
				column: "UserId");

			migrationBuilder.CreateIndex(
				name: "IX_ReelFaces_FaceId",
				table: "ReelFaces",
				column: "FaceId");

			migrationBuilder.CreateIndex(
				name: "IX_ReelFaces_ReelId_FaceId",
				table: "ReelFaces",
				columns: new[] { "ReelId", "FaceId" },
				unique: true);

			migrationBuilder.CreateIndex(
				name: "IX_ReelLikes_ReelId_UserId",
				table: "ReelLikes",
				columns: new[] { "ReelId", "UserId" },
				unique: true);

			migrationBuilder.CreateIndex(
				name: "IX_ReelLikes_UserId",
				table: "ReelLikes",
				column: "UserId");

			migrationBuilder.CreateIndex(
				name: "IX_Reels_CreatorId",
				table: "Reels",
				column: "CreatorId");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "ReelComments");

			migrationBuilder.DropTable(
				name: "ReelFaces");

			migrationBuilder.DropTable(
				name: "ReelLikes");

			migrationBuilder.DropTable(
				name: "Reels");
		}
	}
}
