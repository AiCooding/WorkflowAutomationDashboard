using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowDashboard.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogDirSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AgentsDir",
                table: "AgentRunnerSettings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgentsDir",
                table: "AgentRunnerSettings");
        }
    }
}
