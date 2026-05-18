using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeDemo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageIsPlatformDirectMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPlatformDirectMessage",
                table: "Messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE "Messages" AS m
                SET "IsPlatformDirectMessage" = TRUE
                FROM "AspNetUsers" AS u
                INNER JOIN "UserRoles" AS r ON u."UserRoleId" = r."Id"
                WHERE m."SenderId" = u."Id"
                  AND r."Name" = 'SUPER_ADMIN'
                  AND m."IsMessageRequest" = FALSE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPlatformDirectMessage",
                table: "Messages");
        }
    }
}
