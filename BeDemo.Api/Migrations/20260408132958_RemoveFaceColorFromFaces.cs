using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeDemo.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFaceColorFromFaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure gradient before dropping Color (matches FaceGradientPresets for seeded indices).
            migrationBuilder.Sql(
                """
                UPDATE "Faces" SET "GradientSettings" = '{"type":"linear","colors":["#6366f1","#06b6d4","#a78bfa"],"angle":118,"animation":"rotate","animationSpeed":16}'
                WHERE "Index" = 'public' AND ("GradientSettings" IS NULL OR btrim("GradientSettings") = '');
                UPDATE "Faces" SET "GradientSettings" = '{"type":"linear","colors":["#047857","#34d399","#065f46"],"angle":52,"animation":"shift","animationSpeed":11}'
                WHERE "Index" = 'basic' AND ("GradientSettings" IS NULL OR btrim("GradientSettings") = '');
                UPDATE "Faces" SET "GradientSettings" = '{"type":"linear","colors":["#ea580c","#facc15","#dc2626"],"angle":195,"animation":"pulse","animationSpeed":4.5}'
                WHERE "Index" = 'koncept' AND ("GradientSettings" IS NULL OR btrim("GradientSettings") = '');
                UPDATE "Faces" SET "GradientSettings" = '{"type":"linear","colors":["#7c3aed","#ec4899","#8b5cf6"],"angle":90,"animation":"rotate","animationSpeed":14}'
                WHERE ("GradientSettings" IS NULL OR btrim("GradientSettings") = '');
                """);

            migrationBuilder.DropColumn(
                name: "Color",
                table: "Faces");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "Faces",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
