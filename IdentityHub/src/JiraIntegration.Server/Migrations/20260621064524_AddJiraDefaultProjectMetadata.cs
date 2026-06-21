using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JiraIntegration.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddJiraDefaultProjectMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultIssueTypeName",
                table: "JiraConnections",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DefaultProjectKey",
                table: "JiraConnections",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultIssueTypeName",
                table: "JiraConnections");

            migrationBuilder.DropColumn(
                name: "DefaultProjectKey",
                table: "JiraConnections");
        }
    }
}
