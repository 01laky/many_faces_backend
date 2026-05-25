using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddBlogEntities : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "Blogs",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					CreatorId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					FaceId = table.Column<int>(type: "integer", nullable: false),
					Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
					Content = table.Column<string>(type: "text", nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_Blogs", x => x.Id);
					table.ForeignKey(
						name: "FK_Blogs_AspNetUsers_CreatorId",
						column: x => x.CreatorId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_Blogs_Faces_FaceId",
						column: x => x.FaceId,
						principalTable: "Faces",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict);
				});

			migrationBuilder.CreateTable(
				name: "BlogComments",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					BlogId = table.Column<int>(type: "integer", nullable: false),
					UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_BlogComments", x => x.Id);
					table.ForeignKey(
						name: "FK_BlogComments_AspNetUsers_UserId",
						column: x => x.UserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_BlogComments_Blogs_BlogId",
						column: x => x.BlogId,
						principalTable: "Blogs",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "BlogImages",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					BlogId = table.Column<int>(type: "integer", nullable: false),
					ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
					SortOrder = table.Column<int>(type: "integer", nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_BlogImages", x => x.Id);
					table.ForeignKey(
						name: "FK_BlogImages_Blogs_BlogId",
						column: x => x.BlogId,
						principalTable: "Blogs",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "BlogLikes",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					BlogId = table.Column<int>(type: "integer", nullable: false),
					UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_BlogLikes", x => x.Id);
					table.ForeignKey(
						name: "FK_BlogLikes_AspNetUsers_UserId",
						column: x => x.UserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_BlogLikes_Blogs_BlogId",
						column: x => x.BlogId,
						principalTable: "Blogs",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "IX_BlogComments_BlogId",
				table: "BlogComments",
				column: "BlogId");

			migrationBuilder.CreateIndex(
				name: "IX_BlogComments_UserId",
				table: "BlogComments",
				column: "UserId");

			migrationBuilder.CreateIndex(
				name: "IX_BlogImages_BlogId",
				table: "BlogImages",
				column: "BlogId");

			migrationBuilder.CreateIndex(
				name: "IX_BlogLikes_BlogId_UserId",
				table: "BlogLikes",
				columns: new[] { "BlogId", "UserId" },
				unique: true);

			migrationBuilder.CreateIndex(
				name: "IX_BlogLikes_UserId",
				table: "BlogLikes",
				column: "UserId");

			migrationBuilder.CreateIndex(
				name: "IX_Blogs_CreatorId",
				table: "Blogs",
				column: "CreatorId");

			migrationBuilder.CreateIndex(
				name: "IX_Blogs_FaceId",
				table: "Blogs",
				column: "FaceId");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "BlogComments");

			migrationBuilder.DropTable(
				name: "BlogImages");

			migrationBuilder.DropTable(
				name: "BlogLikes");

			migrationBuilder.DropTable(
				name: "Blogs");
		}
	}
}
