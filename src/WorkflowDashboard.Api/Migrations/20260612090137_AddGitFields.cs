using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowDashboard.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGitFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BranchPrefix",
                table: "PipelineRuns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultBranch",
                table: "PipelineRuns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeatureSlug",
                table: "PipelineRuns",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TicketNumber",
                table: "PipelineRuns",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BranchPrefix",
                table: "PipelineRuns");

            migrationBuilder.DropColumn(
                name: "DefaultBranch",
                table: "PipelineRuns");

            migrationBuilder.DropColumn(
                name: "FeatureSlug",
                table: "PipelineRuns");

            migrationBuilder.DropColumn(
                name: "TicketNumber",
                table: "PipelineRuns");
        }
    }
}
