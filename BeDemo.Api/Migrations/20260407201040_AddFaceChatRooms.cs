using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFaceChatRooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ChatRoomsCreate",
                table: "Faces",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "FaceChatRooms",
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
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastMessageAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaceChatRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaceChatRooms_AspNetUsers_CreatorUserId",
                        column: x => x.CreatorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FaceChatRooms_Faces_FaceId",
                        column: x => x.FaceId,
                        principalTable: "Faces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FaceChatRoomJoinRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FaceChatRoomId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaceChatRoomJoinRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaceChatRoomJoinRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FaceChatRoomJoinRequests_FaceChatRooms_FaceChatRoomId",
                        column: x => x.FaceChatRoomId,
                        principalTable: "FaceChatRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FaceChatRoomMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FaceChatRoomId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaceChatRoomMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaceChatRoomMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FaceChatRoomMembers_FaceChatRooms_FaceChatRoomId",
                        column: x => x.FaceChatRoomId,
                        principalTable: "FaceChatRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FaceChatRoomMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FaceChatRoomId = table.Column<int>(type: "integer", nullable: false),
                    SenderUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Content = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaceChatRoomMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaceChatRoomMessages_AspNetUsers_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FaceChatRoomMessages_FaceChatRooms_FaceChatRoomId",
                        column: x => x.FaceChatRoomId,
                        principalTable: "FaceChatRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FaceChatRoomJoinRequests_FaceChatRoomId_UserId_Status",
                table: "FaceChatRoomJoinRequests",
                columns: new[] { "FaceChatRoomId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FaceChatRoomJoinRequests_UserId",
                table: "FaceChatRoomJoinRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FaceChatRoomMembers_FaceChatRoomId_UserId",
                table: "FaceChatRoomMembers",
                columns: new[] { "FaceChatRoomId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FaceChatRoomMembers_UserId",
                table: "FaceChatRoomMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FaceChatRoomMessages_FaceChatRoomId",
                table: "FaceChatRoomMessages",
                column: "FaceChatRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_FaceChatRoomMessages_SenderUserId",
                table: "FaceChatRoomMessages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FaceChatRoomMessages_SentAt",
                table: "FaceChatRoomMessages",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_FaceChatRooms_CreatorUserId",
                table: "FaceChatRooms",
                column: "CreatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FaceChatRooms_FaceId",
                table: "FaceChatRooms",
                column: "FaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FaceChatRoomJoinRequests");

            migrationBuilder.DropTable(
                name: "FaceChatRoomMembers");

            migrationBuilder.DropTable(
                name: "FaceChatRoomMessages");

            migrationBuilder.DropTable(
                name: "FaceChatRooms");

            migrationBuilder.DropColumn(
                name: "ChatRoomsCreate",
                table: "Faces");
        }
    }
}
