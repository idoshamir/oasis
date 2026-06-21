using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JiraIntegration.Server.Migrations
{
    /// <inheritdoc />
    public partial class RenameApiKeyDescriptionToName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Description",
                table: "ApiKeys",
                newName: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Name",
                table: "ApiKeys",
                newName: "Description");
        }
    }
}
