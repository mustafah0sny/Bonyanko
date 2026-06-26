using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BonyankopAPI.Migrations
{
    public partial class AddCrackVisionFieldsToDiagnostic : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiRawResponseJson",
                table: "Diagnostics",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeatmapImageUrl",
                table: "Diagnostics",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultImageUrl",
                table: "Diagnostics",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiRawResponseJson",
                table: "Diagnostics");

            migrationBuilder.DropColumn(
                name: "HeatmapImageUrl",
                table: "Diagnostics");

            migrationBuilder.DropColumn(
                name: "ResultImageUrl",
                table: "Diagnostics");
        }
    }
}
