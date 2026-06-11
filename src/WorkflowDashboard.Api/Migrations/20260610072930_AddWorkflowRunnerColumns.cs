using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowDashboard.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowRunnerColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename Workflows.Type → Workflows.CatalogSlug (preserve historical slugs).
            migrationBuilder.RenameColumn(
                name: "Type",
                table: "Workflows",
                newName: "CatalogSlug");

            // Add denormalised RepositoryId (required for new rows; dev DB is empty
            // per Phase 1 README — no backfill needed).
            migrationBuilder.AddColumn<string>(
                name: "RepositoryId",
                table: "Workflows",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LaunchPayloadJson",
                table: "Workflows",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessId",
                table: "Workflows",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_RepositoryId",
                table: "Workflows",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_Status",
                table: "Workflows",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_Workflows_Repositories_RepositoryId",
                table: "Workflows",
                column: "RepositoryId",
                principalTable: "Repositories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Workflows_Repositories_RepositoryId",
                table: "Workflows");

            migrationBuilder.DropIndex(
                name: "IX_Workflows_RepositoryId",
                table: "Workflows");

            migrationBuilder.DropIndex(
                name: "IX_Workflows_Status",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "RepositoryId",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "LaunchPayloadJson",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "ProcessId",
                table: "Workflows");

            migrationBuilder.RenameColumn(
                name: "CatalogSlug",
                table: "Workflows",
                newName: "Type");
        }
    }
}
