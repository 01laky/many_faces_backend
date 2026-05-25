using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeDemo.Api.Migrations
{
	/// <inheritdoc />
	public partial class AddOperatorAiMessageLocaleMetadata : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "AuthorEmail",
				table: "OperatorAiMessages",
				type: "character varying(256)",
				maxLength: 256,
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "ResponseLocale",
				table: "OperatorAiMessages",
				type: "character varying(8)",
				maxLength: 8,
				nullable: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "AuthorEmail",
				table: "OperatorAiMessages");

			migrationBuilder.DropColumn(
				name: "ResponseLocale",
				table: "OperatorAiMessages");
		}
	}
}
