using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddAlbumEntities : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "Albums",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					CreatorId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
					Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
					AlbumType = table.Column<int>(type: "integer", nullable: false),
					MediaType = table.Column<int>(type: "integer", nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_Albums", x => x.Id);
					table.ForeignKey(
						name: "FK_Albums_AspNetUsers_CreatorId",
						column: x => x.CreatorId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "AlbumComments",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					AlbumId = table.Column<int>(type: "integer", nullable: false),
					UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_AlbumComments", x => x.Id);
					table.ForeignKey(
						name: "FK_AlbumComments_Albums_AlbumId",
						column: x => x.AlbumId,
						principalTable: "Albums",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_AlbumComments_AspNetUsers_UserId",
						column: x => x.UserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "AlbumFaces",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					AlbumId = table.Column<int>(type: "integer", nullable: false),
					FaceId = table.Column<int>(type: "integer", nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_AlbumFaces", x => x.Id);
					table.ForeignKey(
						name: "FK_AlbumFaces_Albums_AlbumId",
						column: x => x.AlbumId,
						principalTable: "Albums",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_AlbumFaces_Faces_FaceId",
						column: x => x.FaceId,
						principalTable: "Faces",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict);
				});

			migrationBuilder.CreateTable(
				name: "AlbumLikes",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					AlbumId = table.Column<int>(type: "integer", nullable: false),
					UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_AlbumLikes", x => x.Id);
					table.ForeignKey(
						name: "FK_AlbumLikes_Albums_AlbumId",
						column: x => x.AlbumId,
						principalTable: "Albums",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_AlbumLikes_AspNetUsers_UserId",
						column: x => x.UserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "IX_AlbumComments_AlbumId",
				table: "AlbumComments",
				column: "AlbumId");

			migrationBuilder.CreateIndex(
				name: "IX_AlbumComments_UserId",
				table: "AlbumComments",
				column: "UserId");

			migrationBuilder.CreateIndex(
				name: "IX_AlbumFaces_AlbumId_FaceId",
				table: "AlbumFaces",
				columns: new[] { "AlbumId", "FaceId" },
				unique: true);

			migrationBuilder.CreateIndex(
				name: "IX_AlbumFaces_FaceId",
				table: "AlbumFaces",
				column: "FaceId");

			migrationBuilder.CreateIndex(
				name: "IX_AlbumLikes_AlbumId_UserId",
				table: "AlbumLikes",
				columns: new[] { "AlbumId", "UserId" },
				unique: true);

			migrationBuilder.CreateIndex(
				name: "IX_AlbumLikes_UserId",
				table: "AlbumLikes",
				column: "UserId");

			migrationBuilder.CreateIndex(
				name: "IX_Albums_CreatorId",
				table: "Albums",
				column: "CreatorId");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "AlbumComments");

			migrationBuilder.DropTable(
				name: "AlbumFaces");

			migrationBuilder.DropTable(
				name: "AlbumLikes");

			migrationBuilder.DropTable(
				name: "Albums");
		}
	}
}
