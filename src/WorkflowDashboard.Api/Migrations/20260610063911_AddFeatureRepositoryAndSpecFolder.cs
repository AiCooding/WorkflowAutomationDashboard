using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowDashboard.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureRepositoryAndSpecFolder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SpecPath",
                table: "Features",
                newName: "SpecFolder");

            migrationBuilder.AddColumn<string>(
                name: "RepositoryId",
                table: "Features",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Features_RepositoryId",
                table: "Features",
                column: "RepositoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Features_Repositories_RepositoryId",
                table: "Features",
                column: "RepositoryId",
                principalTable: "Repositories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Features_Repositories_RepositoryId",
                table: "Features");

            migrationBuilder.DropIndex(
                name: "IX_Features_RepositoryId",
                table: "Features");

            migrationBuilder.DropColumn(
                name: "RepositoryId",
                table: "Features");

            migrationBuilder.RenameColumn(
                name: "SpecFolder",
                table: "Features",
                newName: "SpecPath");
        }
    }
}
