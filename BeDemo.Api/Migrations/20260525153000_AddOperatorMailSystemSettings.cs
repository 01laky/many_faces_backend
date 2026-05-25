using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeDemo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOperatorMailSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperatorMailSystemSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultLocale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    WorkerGrpcUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    WorkerAuthTokenCiphertext = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    SmtpHost = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SmtpPort = table.Column<int>(type: "integer", nullable: false),
                    SmtpStartTls = table.Column<bool>(type: "boolean", nullable: false),
                    SmtpUser = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SmtpPasswordCiphertext = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    FromEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    FromDisplayName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PortalPublicBaseUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CompleteRegistrationPathTemplate = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    MobileDeepLinkBase = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    PreferMobileDeepLinkWhenPlatformMobile = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorMailSystemSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperatorMailSystemSettings");
        }
    }
}
