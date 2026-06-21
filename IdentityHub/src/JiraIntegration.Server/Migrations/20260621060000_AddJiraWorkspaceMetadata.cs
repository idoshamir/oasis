using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JiraIntegration.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddJiraWorkspaceMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WorkspaceName",
                table: "JiraConnections",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WorkspaceUrl",
                table: "JiraConnections",
                type: "TEXT",
                maxLength: 512,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WorkspaceName",
                table: "JiraConnections");

            migrationBuilder.DropColumn(
                name: "WorkspaceUrl",
                table: "JiraConnections");
        }
    }
}
