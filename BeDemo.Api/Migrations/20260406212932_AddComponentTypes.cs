using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddComponentTypes : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "ComponentTypes",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false),
					Index = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
					Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
					CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_ComponentTypes", x => x.Id);
				});

			migrationBuilder.CreateIndex(
				name: "IX_ComponentTypes_Index",
				table: "ComponentTypes",
				column: "Index",
				unique: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "ComponentTypes");
		}
	}
}
