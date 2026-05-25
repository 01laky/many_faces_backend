using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddFaceWallTickets : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "FaceWallTickets",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					FaceId = table.Column<int>(type: "integer", nullable: false),
					CreatorUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
					Description = table.Column<string>(type: "text", nullable: false),
					Status = table.Column<int>(type: "integer", nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_FaceWallTickets", x => x.Id);
					table.ForeignKey(
						name: "FK_FaceWallTickets_AspNetUsers_CreatorUserId",
						column: x => x.CreatorUserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_FaceWallTickets_Faces_FaceId",
						column: x => x.FaceId,
						principalTable: "Faces",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "FaceWallTicketComments",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					FaceWallTicketId = table.Column<int>(type: "integer", nullable: false),
					UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					Content = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_FaceWallTicketComments", x => x.Id);
					table.ForeignKey(
						name: "FK_FaceWallTicketComments_AspNetUsers_UserId",
						column: x => x.UserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_FaceWallTicketComments_FaceWallTickets_FaceWallTicketId",
						column: x => x.FaceWallTicketId,
						principalTable: "FaceWallTickets",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "FaceWallTicketLikes",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					FaceWallTicketId = table.Column<int>(type: "integer", nullable: false),
					UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_FaceWallTicketLikes", x => x.Id);
					table.ForeignKey(
						name: "FK_FaceWallTicketLikes_AspNetUsers_UserId",
						column: x => x.UserId,
						principalTable: "AspNetUsers",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_FaceWallTicketLikes_FaceWallTickets_FaceWallTicketId",
						column: x => x.FaceWallTicketId,
						principalTable: "FaceWallTickets",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "IX_FaceWallTicketComments_FaceWallTicketId",
				table: "FaceWallTicketComments",
				column: "FaceWallTicketId");

			migrationBuilder.CreateIndex(
				name: "IX_FaceWallTicketComments_UserId",
				table: "FaceWallTicketComments",
				column: "UserId");

			migrationBuilder.CreateIndex(
				name: "IX_FaceWallTicketLikes_FaceWallTicketId_UserId",
				table: "FaceWallTicketLikes",
				columns: new[] { "FaceWallTicketId", "UserId" },
				unique: true);

			migrationBuilder.CreateIndex(
				name: "IX_FaceWallTicketLikes_UserId",
				table: "FaceWallTicketLikes",
				column: "UserId");

			migrationBuilder.CreateIndex(
				name: "IX_FaceWallTickets_CreatorUserId",
				table: "FaceWallTickets",
				column: "CreatorUserId");

			migrationBuilder.CreateIndex(
				name: "IX_FaceWallTickets_FaceId",
				table: "FaceWallTickets",
				column: "FaceId");

			migrationBuilder.CreateIndex(
				name: "IX_FaceWallTickets_FaceId_CreatorUserId",
				table: "FaceWallTickets",
				columns: new[] { "FaceId", "CreatorUserId" });
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "FaceWallTicketComments");

			migrationBuilder.DropTable(
				name: "FaceWallTicketLikes");

			migrationBuilder.DropTable(
				name: "FaceWallTickets");
		}
	}
}
