using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowDashboard.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDisableGitCommitToPipelineRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DisableGitCommit",
                table: "PipelineRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisableGitCommit",
                table: "PipelineRuns");
        }
    }
}
