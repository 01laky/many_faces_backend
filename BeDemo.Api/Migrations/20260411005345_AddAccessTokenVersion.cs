using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddAccessTokenVersion : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<int>(
				name: "AccessTokenVersion",
				table: "AspNetUsers",
				type: "integer",
				nullable: false,
				defaultValue: 0);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "AccessTokenVersion",
				table: "AspNetUsers");
		}
	}
}
